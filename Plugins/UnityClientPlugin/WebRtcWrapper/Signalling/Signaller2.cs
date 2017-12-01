using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebRtcWrapper.Utilities;

namespace WebRtcWrapper.Signalling
{
	public class Signaller2 : ISignaller
	{
		/// <summary>
		/// The value we set <see cref="Id"/> to when <see cref="IsConnected"/> is false
		/// </summary>
		public static readonly int DisconnectedId = -1;

		private class Request : ISimpleHttpRequest
		{
			public Uri Uri { get; set; }

			public WebHeaderCollection Headers { get; set; }

			public string Body { get; set; }
		}

		public int Id { get; private set; }
		public bool IsConnected { get; private set; }
		public int HeartbeatMs { get; set; }
		public string AuthenticationHeader { get; set; }

		public event ServerConnectionFailureDelegate OnServerConnectionFailure;
		public event ConnectedDelegate OnConnected;
		public event DisconnectedDelegate OnDisconnected;
		public event PeerConnectedDelegate OnPeerConnected;
		public event PeerDisonnectedDelegate OnPeerDisconnected;
		public event PeerHangupDelegate OnPeerHangup;
		public event MessageFromPeerDelegate OnMessageFromPeer;

		private ISimpleHttpClient httpClient;
		private Uri connectedUri;
		private Dictionary<int, string> connectedPeers;
		private CancellableTask waitEndpointTask;
		private CancellableTask heartbeatEndpointTask;

		public Signaller2(ISimpleHttpClient httpClient)
		{
			this.httpClient = httpClient;
			this.connectedPeers = new Dictionary<int, string>();
			this.Id = Signaller2.DisconnectedId;

			// attach a listener for starting the http threads
			this.OnConnected += () =>
			{
				this.StartBackgroundHttp();
			};

			// attach a listener for stopping the http threads
			this.OnDisconnected += () =>
			{
				this.StopBackgroundHttp();
			};
		}
		
		public async Task<bool> ConnectAsync(string uri, string client_name)
		{
			if (this.IsConnected)
			{
				throw new InvalidOperationException("Already connected");
			}

			var attemptUri = new Uri(uri);
			var attemptHeaders = new WebHeaderCollection
			{
				[HttpRequestHeader.Authorization] = this.AuthenticationHeader
			};

			try
			{
				var res = await this.httpClient.GetAsync(new Request()
				{
					Uri = new Uri(attemptUri, $"/sign_in?peer_name={client_name}"),
					Headers = attemptHeaders
				});

				if (res.Status != HttpStatusCode.OK)
				{
					// this will be caught below, and result in a failure to connect
					throw new SimpleHttpClientException(new WebException($"Invalid StatusCode '{res.Status}'",
						WebExceptionStatus.ServerProtocolViolation));
				}

				// since we're connecting, the Pragma id we get back indicates our assigned id
				this.Id = int.Parse(res.Headers[HttpRequestHeader.Pragma]);

				// handle the response data, emitting events as needed
				this.ParseResponse(res.Body, this.Id);

				// everything went great, we are now connected
				this.IsConnected = true;

				// fire the connected event
				this.OnConnected?.Invoke();

				// succeed
				return true;
			}
			catch (SimpleHttpClientException ex)
			{
				this.OnServerConnectionFailure?.Invoke(ex);

				return false;
			}
		}

		public async Task<bool> DisconnectAsync()
		{
			if (!this.IsConnected)
			{
				throw new InvalidOperationException("Already disconnected");
			}
			
			var attemptHeaders = new WebHeaderCollection
			{
				[HttpRequestHeader.Authorization] = this.AuthenticationHeader
			};
			
			var res = await this.httpClient.GetAsync(new Request()
			{
				Uri = new Uri(this.connectedUri, $"/sign_out?peer_id={this.Id}"),
				Headers = attemptHeaders
			});

			if (res.Status != HttpStatusCode.OK)
			{
				// we couldn't disconnect successfully
				return false;
			}
			
			// everything went great, we are now disconnected
			this.IsConnected = false;
			this.Id = Signaller2.DisconnectedId;

			// fire the connected event
			this.OnDisconnected?.Invoke();

			// succeed
			return true;
		}

		private void StartBackgroundHttp()
		{
			// describe (and start running) the /wait task
			this.waitEndpointTask = CancellableTask.Run(async () =>
			{
				var attemptHeaders = new WebHeaderCollection
				{
					[HttpRequestHeader.Authorization] = this.AuthenticationHeader
				};

				var res = await this.httpClient.GetAsync(new Request()
				{
					Uri = new Uri(this.connectedUri, $"/wait?peer_id={this.Id}"),
					Headers = attemptHeaders
				});

				if (res.Status != HttpStatusCode.OK)
				{
					// TODO(bengreenier): this might be good to log
					return;
				}
				
				var messageId = int.Parse(res.Headers[HttpRequestHeader.Pragma]);

				// handle the response data, emitting events as needed
				this.ParseResponse(res.Body, messageId);
			});

			// describe (and start running) the /heartbeat task
			this.heartbeatEndpointTask = CancellableTask.Run(async () =>
			{
				var attemptHeaders = new WebHeaderCollection
				{
					[HttpRequestHeader.Authorization] = this.AuthenticationHeader
				};

				var res = await this.httpClient.GetAsync(new Request()
				{
					Uri = new Uri(this.connectedUri, $"/heartbeat?peer_id={this.Id}"),
					Headers = attemptHeaders
				});
				
				// TODO(bengreenier): might be good to log if res.Status != HttpStatusCode.OK
				return;
			});
		}

		private void StopBackgroundHttp()
		{
			this.waitEndpointTask?.Cancel();
			this.waitEndpointTask?.Task.Wait();

			this.heartbeatEndpointTask?.Cancel();
			this.heartbeatEndpointTask?.Task.Wait();
		}

		/// <summary>
		/// Parses a body and updates peers, emitting events as needed
		/// </summary>
		/// <param name="messageBody">the body of the message</param>
		/// <param name="messageId">the id of the message</param>
		/// <returns>success indicator</returns>
		private void ParseResponse(string messageBody, int messageId)
		{
			// if pragmaId isn't us, it's not a notification it's a message
			if (messageId != this.Id)
			{
				// if the entire message is bye, they gone
				if (messageBody == "BYE")
				{
					this.OnPeerHangup?.Invoke(messageId);
				}
				// otherwise, we share the message
				else
				{
					this.OnMessageFromPeer?.Invoke(messageId, messageBody);
				}
			}

			// oh boy, new peers
			var oldPeers = new Dictionary<int, string>(this.connectedPeers);

			// peer updates aren't incremental
			this.connectedPeers.Clear();

			foreach (var line in messageBody.Split('\n'))
			{
				var parts = line.Split(',');

				if (parts.Length != 3)
				{
					continue;
				}

				var id = int.Parse(parts[1]);

				// indicates the peer is connected
				// and isn't us
				if (int.Parse(parts[2]) == 1 && id != this.Id)
				{
					this.connectedPeers[id] = parts[0];
				}
			}

			foreach (var peer in this.connectedPeers)
			{
				if (!oldPeers.ContainsKey(peer.Key))
				{
					this.OnPeerConnected?.Invoke(peer.Key, peer.Value);
				}
				oldPeers.Remove(peer.Key);
			}

			// handles disconnects that aren't respectful (and don't send /sign_out)
			foreach (var oldPeer in oldPeers)
			{
				this.OnPeerDisconnected?.Invoke(oldPeer.Key);
			}
		}

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.StopBackgroundHttp();
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion
	}
}
