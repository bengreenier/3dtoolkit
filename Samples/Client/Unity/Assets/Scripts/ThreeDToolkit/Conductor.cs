using System;
using System.Collections.Generic;
using ThreeDToolkit.Interfaces;

namespace ThreeDToolkit
{
    public class Conductor : IConductor
    {
        public ISignaller Signaller
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

        public IList<IIceServer> IceServers
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        
        public event Action StreamAdded;
        public event Action StreamRemoved;
        public event Action PeerConnectionCreated;
        public event Action PeerConnectionDestroyed;
        public event Action<Exception> Error;
        public event Action<int, string> PeerMessage;
        public event Action<int, string> PeerData;
        public event Action<IConnectionStatistics> ConnectionStatus;

        public void CreatePeerConnection(string sdpOffer)
        {
            throw new NotImplementedException();
        }

        public string CreateSdpOffer()
        {
            throw new NotImplementedException();
        }

        public bool TryGetTexture(out IntPtr texturePtr)
        {
            throw new NotImplementedException();
        }
    }
}
