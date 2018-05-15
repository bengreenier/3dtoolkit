using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using WebRtcWrapper.Signalling;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace WebRtcWrapper.IntegrationTests
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private Signaller signaller;
		private string uri;
		private string name;
		private int heartbeat;
		private List<KeyValuePair<int, string>> peers;
		private bool firstNavigation;

		public MainPage()
		{
			this.firstNavigation = true;
			this.InitializeComponent();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (!this.firstNavigation)
			{
				return;
			}

			this.firstNavigation = false;

			var args = ((string)e.Parameter).Split(' ');

			this.uri = args[0];
			this.name = args[1];
			this.heartbeat = int.Parse(args[2]);

			this.peers = new List<KeyValuePair<int, string>>();
			this.signaller = new Signaller(new WebRequestHttpClient());
			this.signaller.HeartbeatMs = this.heartbeat;

			this.signaller.OnConnected += () =>
			{
				RunOnUIThread(() =>
				{
					this.StatusText.Text += "OnConnected";
				});
			};

			this.signaller.OnDisconnected += () =>
			{
				RunOnUIThread(() =>
				{
					this.StatusText.Text += "OnDisconnected";
				});
			};

			this.signaller.OnMessageFromPeer += (int peer, string msg) =>
			{
				RunOnUIThread(() =>
				{
					this.RecieveData.Text += $"Peer<{peer}> sent:\n{msg}\n=============================================\n";
				});
			};

			this.signaller.OnPeerConnected += (int peer, string name) =>
			{
				RunOnUIThread(() =>
				{
					this.peers.Add(new KeyValuePair<int, string>(peer, name));

					this.StatusText.Text += string.Join(",", this.peers);
				});
			};

			this.signaller.OnPeerDisconnected += (int peer) =>
			{
				RunOnUIThread(() =>
				{
					this.peers.RemoveAll(p => p.Key == peer);

					this.StatusText.Text += string.Join(",", this.peers);
				});
			};

			this.signaller.OnPeerHangup += (int peer) =>
			{
				RunOnUIThread(() =>
				{
					this.peers.RemoveAll(p => p.Key == peer);

					this.StatusText.Text += string.Join(",", this.peers);
				});
			};

			this.signaller.OnServerConnectionFailure += (Exception ex) =>
			{
				RunOnUIThread(() =>
				{
					this.StatusText.Text += $"ConnectionFailure: {ex.Message}\n{ex.StackTrace}";
				});
			};

		}

		private async void RunOnUIThread(Action uiWork)
		{
			await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(uiWork));
		}

		private async void ToggleConnectionButton_Click(object sender, RoutedEventArgs e)
		{
			bool status;
			string buttonText;

			if (this.signaller.IsConnected)
			{
				status = await this.signaller.DisconnectAsync();
				buttonText = "Connect";
			}
			else
			{
				status = await this.signaller.ConnectAsync(this.uri, this.name);
				buttonText = "Disconnect";
			}

			if (!status)
			{
				this.StatusText.Text += $"Failed ToggleConnection: {!this.signaller.IsConnected}\n";
			}
			else
			{
				this.ToggleConnectionButton.Content = buttonText;
				this.StatusText.Text += $"Succeeded ToggleConnection: {this.signaller.IsConnected}\n";
			}
		}

		private void ClearReceived_Click(object sender, RoutedEventArgs e)
		{
			this.RecieveData.Text = "";
		}

		private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
		{
			var status = await this.signaller.SendAsync(int.Parse(this.MessageId.Text), this.MessageData.Text);

			this.StatusText.Text += (status ? "Succeeded" : "Failed") + " SendMessage";
		}
	}
}
