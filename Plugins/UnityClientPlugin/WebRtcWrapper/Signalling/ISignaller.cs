using System;
using System.Threading.Tasks;

namespace WebRtcWrapper.Signalling
{
	public delegate void ConnectedDelegate();
	public delegate void DisconnectedDelegate();
	public delegate void PeerConnectedDelegate(int id, string name);
	public delegate void PeerDisonnectedDelegate(int peer_id);
	public delegate void PeerHangupDelegate(int peer_id);
	public delegate void MessageFromPeerDelegate(int peer_id, string message);
	public delegate void MessageSentDelegate(int err);
	public delegate void ServerConnectionFailureDelegate(Exception error);
	
	/// <summary>
	/// Represents a Signaller
	/// </summary>
	public interface ISignaller : IDisposable
	{
		event ServerConnectionFailureDelegate OnServerConnectionFailure;
		event ConnectedDelegate OnConnected;
		event DisconnectedDelegate OnDisconnected;
		event PeerConnectedDelegate OnPeerConnected;
		event PeerDisonnectedDelegate OnPeerDisconnected;
		event PeerHangupDelegate OnPeerHangup;
		event MessageFromPeerDelegate OnMessageFromPeer;

		int Id { get; }
		bool IsConnected { get; }
		int HeartbeatMs { get; set; }
		string AuthenticationHeader { get; set; }

		Task<bool> ConnectAsync(string uri, string client_name);
		Task<bool> SendAsync(int peerId, string message);
		Task<bool> DisconnectAsync();
	}
}
