using System;
using System.Linq;
using System.Net;
using System.Net.PeerToPeer;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using System.Net.PeerToPeer.Collaboration;

namespace P2P
{
	public partial class MainWindow
	{
		private P2PService _service;
		private ServiceHost _host;
		private PeerName _pn;
		private PeerNameRegistration _pnr;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				var port = ConfigurationManager.AppSettings["port"];
				var username = ConfigurationManager.AppSettings["username"];
			
				Title = $"P2P приложение - {username}";

				var dns = Dns.GetHostAddresses(Dns.GetHostName());
				
				var url = (from address in dns
						where address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
						select $"net.tcp://{address}:{port}/P2PService").FirstOrDefault();

				MessageBox.Show(url);
			
				if (url == null)
				{
					MessageBox.Show(this, "Не удается определить адрес конечной точки WCF.", "Networking Error",
						MessageBoxButton.OK, MessageBoxImage.Stop);
					Application.Current.Shutdown();
				}
			
				_service = new P2PService(this, username);
				_host = new ServiceHost(_service, new Uri(url));
				var binding = new NetTcpBinding {Security = {Mode = SecurityMode.None}};
				_host.AddServiceEndpoint(typeof(IP2PService), binding, url);

				try
				{
					_host.Open();
				}
				catch (AddressAlreadyInUseException)
				{
					MessageBox.Show(this, "Не удается начать прослушивание, порт занят.", "WCF Error",
						MessageBoxButton.OK, MessageBoxImage.Stop);
					Application.Current.Shutdown();
				}
			
				_pn = new PeerName("0.P2P Sample");
				_pnr = new PeerNameRegistration(_pn, int.Parse(port))
				{
					Cloud = Cloud.Available,
					Comment = "Simple p2p app untitled"
				};
			
				_pnr.Start();
				MessageBox.Show("Started!");
			}
			catch (Exception exception)
			{
				MessageBox.Show(exception.Message);
				MessageBox.Show(exception.StackTrace);
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_pnr.Stop();
			_host.Close();
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var resolver = new PeerNameResolver();
				resolver.ResolveProgressChanged += resolver_ResolveProgressChanged;
				resolver.ResolveCompleted += resolver_ResolveCompleted;
			
				PeerList.Items.Clear();
				RefreshButton.IsEnabled = false;

				resolver.ResolveAsync(new PeerName("0.P2P Sample"), Cloud.Available, 20);
			}
			catch (Exception exception)
			{
				MessageBox.Show(exception.Message);
				MessageBox.Show(exception.StackTrace);
			}
		}

		private void resolver_ResolveCompleted(object sender, ResolveCompletedEventArgs e)
		{
			if (PeerList.Items.Count == 0)
			{
				PeerList.Items.Add(new PeerEntry
				{
					DisplayString = "Пиры не найдены.",
					ButtonsEnabled = false
				});
			}
			RefreshButton.IsEnabled = true;
		}

		private void resolver_ResolveProgressChanged(object sender, ResolveProgressChangedEventArgs e)
		{
			var peer = e.PeerNameRecord;

			foreach (var ep in peer.EndPointCollection)
			{
				if (ep.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
				try
				{
					string endpointUrl = $"net.tcp://{ep.Address}:{ep.Port}/P2PService";
					var binding = new NetTcpBinding { Security = { Mode = SecurityMode.None } };
					var serviceProxy = ChannelFactory<IP2PService>.CreateChannel(
						binding, new EndpointAddress(endpointUrl));
					PeerList.Items.Add(
						new PeerEntry
						{
							PeerName = peer.PeerName,
							ServiceProxy = serviceProxy,
							DisplayString = serviceProxy.GetName(),
							ButtonsEnabled = true
						});
				}
				catch (EndpointNotFoundException)
				{
					PeerList.Items.Add(
						new PeerEntry
						{
							PeerName = peer.PeerName,
							DisplayString = "Неизвестный пир",
							ButtonsEnabled = false
						});
				}
			}
		}

		private void PeerList_Click(object sender, RoutedEventArgs e)
		{
			if (((Button) e.OriginalSource).Name != "MessageButton") return;
			var peerEntry = ((Button)e.OriginalSource).DataContext as PeerEntry;
			if (peerEntry?.ServiceProxy == null) return;
			try
			{
				peerEntry.ServiceProxy.SendMessage("Message!", ConfigurationManager.AppSettings["username"]);
			}
			catch (CommunicationException exception)
			{
				MessageBox.Show(exception.Message);
			}
		}

		internal void DisplayMessage(string message, string from)
		{
			Messages.Text += $"{Environment.NewLine} From {@from}: {message}";
		}
	}
}
