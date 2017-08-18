using System;
using System.Runtime.InteropServices;
using ThreeDToolkit.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeDToolkit
{
    public class ThreeDControl : MonoBehaviour
    {
#if UNITY_WSA
        [DllImport("ThreeDToolkit.UWP")]
        private static extern IConductor GetConductor();

        [DllImport("ThreeDToolkit.UWP")]
        private static extern ISignaller GetSignaller();

// add support for other platforms here
// see https://docs.unity3d.com/Manual/PlatformDependentCompilation.html
#else
        private IConductor GetConductor() { throw new EntryPointNotFoundException(); }
        private ISignaller GetSignaller() { throw new EntryPointNotFoundException(); }
#endif

        public enum RuntimeMode
        {
            Auto,
            Native,
            Unity
        }

        public RuntimeMode Mode = RuntimeMode.Auto;

        public RawImage LeftEye;

        public RawImage RightEye;

        public string ClientName;

        public string ServerUri;

        public string TurnUri;

        public string TurnUsername;

        public string TurnPassword;

        public bool ConnectOnAwake = true;

        public IConductor Conductor
        {
            get;
            private set;
        }

        public ISignaller Signaller
        {
            get;
            private set;
        }

        public void Connect()
        {
            this.Signaller.Connect(this.ServerUri, this.ServerUri.StartsWith("https://") ? 443 : 80, this.ClientName);
        }

        public void Disconnect()
        {
            this.Signaller.Disconnect();
        }

        private void Awake()
        {
            switch(this.Mode)
            {
                case RuntimeMode.Auto:
                    if (!TryLoadNative(andThrow: false))
                    {
                        this.Conductor = new Conductor();
                        this.Signaller = new Signaller();
                    }
                    break;
                case RuntimeMode.Native:
                    TryLoadNative();
                    break;
                case RuntimeMode.Unity:
                    this.Conductor = new Conductor();
                    this.Signaller = new Signaller();
                    break;
                default:
                    break;
            }

            if (this.ConnectOnAwake)
            {
                Connect();
            }
        }
        
        private bool TryLoadNative(bool andThrow = true)
        {
            bool ret = true;

            Action<Exception> handle = (Exception ex) =>
            {
                if (andThrow)
                {
                    throw ex;
                }

                ret = false;
            };

            try
            {
                this.Conductor = GetConductor();
                this.Signaller = GetSignaller();
            }
            catch (DllNotFoundException ex)
            {
                handle(ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                handle(ex);
            }

            return ret;
        }
    }
}
