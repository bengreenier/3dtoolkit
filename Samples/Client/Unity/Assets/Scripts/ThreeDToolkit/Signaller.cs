using System;
using System.Collections.Generic;
using ThreeDToolkit.Interfaces;

namespace ThreeDToolkit
{
    public class Signaller : ISignaller
    {
        public IEnumerable<IPeer> Peers
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int HeartbeatInterval
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public IDictionary<string, string> HttpHeaders
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event Action Connected;
        public event Action<int> Disconnected;
        public event Action<Exception> Error;
        public event Action<int> Heartbeat;
        public event Action<IPeer, string> Message;
        public event Action<IPeer> PeerConnected;
        public event Action<IPeer> PeerDisconnected;

        public void Connect(string hostname, int port, string peerName)
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public void Send(IPeer peer, string message)
        {
            throw new NotImplementedException();
        }
    }
}
