using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThreeDToolkit.Interfaces
{
    public interface ISignaller
    {
        event Action Connected;
        event Action<int> Disconnected;
        event Action<Exception> Error;
        event Action<int> Heartbeat;
        event Action<IPeer, string> Message;
        event Action<IPeer> PeerConnected;
        event Action<IPeer> PeerDisconnected;

        IEnumerable<IPeer> Peers
        {
            get;
        }

        int HeartbeatInterval
        {
            get;
            set;
        }

        IDictionary<string, string> HttpHeaders
        {
            get;
        }

        void Connect(string hostname, int port, string peerName);

        void Send(IPeer peer, string message);

        void Disconnect();
    }
}
