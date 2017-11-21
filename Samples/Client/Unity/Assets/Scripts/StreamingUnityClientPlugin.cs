using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_WSA && !UNITY_EDITOR
using System.Linq;
using Org.WebRtc;
using WebRtcWrapper;
using PeerConnectionClient.Signalling;
using Windows.Media.Playback;
using Windows.Media.Core;
#endif

public class StreamingUnityClientPlugin : IDisposable
{
    /// <summary>
    /// The name of the playback plugin from which we dllimport
    /// </summary>
    private const string PlaybackPluginName = "MediaEngineUWP";

    /// <summary>
    /// the default framerate of the plugin
    /// </summary>
    private const uint DefaultFramerate = 30;

#if UNITY_WSA && !UNITY_EDITOR
    private WebRtcControl webrtcControl;
    private MediaVideoTrack videoTrack;
#endif

    public bool HasVideoTrack
    {
        get;
        private set;
    }

    // These events are marshalled from the underlying framework components

    public event GenericDelegate<int, string>.Handler PeerConnect;
    public event GenericDelegate<int>.Handler PeerDisconnect;
    public event GenericDelegate<string>.Handler SignIn;
    public event GenericDelegate.Handler Disconnect;
    public event GenericDelegate<int, string>.Handler MessageFromPeer;
    public event GenericDelegate<Exception>.Handler ServerConnectionFailure;
    public event GenericDelegate<string>.Handler AddStream;
    public event GenericDelegate<string>.Handler RemoveStream;
    public event GenericDelegate<int>.Handler IceConnectionChange;
    public event GenericDelegate<string>.Handler IceCandidate;
    public event GenericDelegate<long>.Handler NetworkLatencyChange;

    private void OnPeerConnected(int val0, string val1) { ErrorOnFailure(() => { if (PeerConnect != null) this.PeerConnect(val0, val1); }); }
    private void OnPeerDisconnected(int val0) { ErrorOnFailure(() => { if (this.PeerDisconnect != null) this.PeerDisconnect(val0); }); }
    private void OnSignedIn(string val0) { ErrorOnFailure(() => { if (this.SignIn != null) this.SignIn(val0); }); }
    private void OnDisconnected() { ErrorOnFailure(() => { if (this.Disconnect != null) this.Disconnect(); }); }
    private void OnMessageFromPeer(int val0, string val1) { ErrorOnFailure(() => { if (MessageFromPeer != null) this.MessageFromPeer(val0, val1); }); }
    private void OnServerConnectionFailure(Exception ex) { ErrorOnFailure(() => { if (this.ServerConnectionFailure != null) this.ServerConnectionFailure(ex); }); }
    private void OnAddStream(string val0) { ErrorOnFailure(() => { if (this.AddStream != null) this.AddStream(val0); }); }
    private void OnRemoveStream(string val0) { ErrorOnFailure(() => { if (this.RemoveStream != null) this.RemoveStream(val0); }); }
    private void OnIceConnectionChange(int val0) { ErrorOnFailure(() => { if (this.IceConnectionChange != null) this.IceConnectionChange(val0); }); }
    private void OnIceCandidate(string val0) { ErrorOnFailure(() => { if (this.IceCandidate != null) this.IceCandidate(val0); }); }
    private void OnNetworkLatencyChange(long val0) { ErrorOnFailure(() => { if (this.NetworkLatencyChange != null) this.NetworkLatencyChange(val0); }); }

    /// <summary>
    /// Fired when a managed error occurs but is triggered by native code
    /// </summary>
    /// <remarks>
    /// This occurs, for example, when an event handler throws, but the event was 
    /// raised by native code
    /// </remarks>
    public event GenericDelegate<Exception>.Handler Error;
    
    /// <summary>
    /// Default ctor
    /// </summary>
    public StreamingUnityClientPlugin()
    {
        // start with no video track
        this.HasVideoTrack = false;

        // initialize the playback plugin
        Native.CreateMediaPlayback();

#if UNITY_WSA && !UNITY_EDITOR
        webrtcControl = new WebRtcControl();

        webrtcControl.OnInitialized += WebrtcControl_OnInitialized;

        // initialize the webrtc plugin
        webrtcControl.Initialize();
#endif
    }

    public bool SendDataChannelMessage(string message)
    {
        if (!this.HasVideoTrack)
        {
            return false;
        }

        return false;
    }

    public void Play(RawImage playbackImage, uint framerate = DefaultFramerate)
    {
        if (!this.HasVideoTrack)
        {
            throw new InvalidOperationException("WebRTC has not yet negotiated video content");
        }

#if UNITY_WSA && !UNITY_EDITOR
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, framerate, "media");
        Native.LoadMediaStreamSource((MediaStreamSource)source);
#endif
        IntPtr nativeTex = IntPtr.Zero;
        Native.GetPrimaryTexture((uint)playbackImage.rectTransform.rect.width,
            (uint)playbackImage.rectTransform.rect.height,
            out nativeTex);

        playbackImage.texture = Texture2D.CreateExternalTexture((int)playbackImage.rectTransform.rect.width,
            (int)playbackImage.rectTransform.rect.height,
            TextureFormat.BGRA32,
            false,
            false,
            nativeTex);

        Native.Play();
    }

    public void Stop()
    {
        Native.Stop();
    }

    private void WebrtcControl_OnInitialized()
    {
#if UNITY_WSA && !UNITY_EDITOR
        // since we know we're initialized, we can now map all the events
        // from the underlying components that we need to surface here
        //
        // TODO(bengreenier): if we had the right abstractions in our webrtc module
        // this code would not need to access the internals of the module - we should
        // refactor that wrapper to facilitate this need

        Conductor.Instance.Signaller.OnPeerConnected += this.OnPeerConnected;
        Conductor.Instance.Signaller.OnPeerDisconnected += this.OnPeerDisconnected;
        Conductor.Instance.Signaller.OnSignedIn += this.OnSignedIn;
        Conductor.Instance.Signaller.OnDisconnected += this.OnDisconnected;
        Conductor.Instance.Signaller.OnMessageFromPeer += this.OnMessageFromPeer;
        Conductor.Instance.Signaller.OnServerConnectionFailure += this.OnServerConnectionFailure;
        Conductor.Instance.OnAddLocalStream += (MediaStreamEvent evt) =>
        {
            this.videoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if (this.videoTrack != null)
            {
                this.HasVideoTrack = true;
            }
            this.OnAddStream(evt.Stream.Id);
        };
        Conductor.Instance.OnAddRemoteStream += (MediaStreamEvent evt) =>
        {
            this.videoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if (this.videoTrack != null)
            {
                this.HasVideoTrack = true;
            }
            this.OnAddStream(evt.Stream.Id);
        };
        Conductor.Instance.OnRemoveRemoteStream += (MediaStreamEvent evt) => { this.OnRemoveStream(evt.Stream.Id); };
        Conductor.Instance.OnIceCandidate += (RTCPeerConnectionIceEvent evt) => { this.OnIceCandidate(evt.Candidate.Candidate); };
        Conductor.Instance.OnIceConnectionChange += (RTCPeerConnectionIceStateChangeEvent evt) => { this.OnIceConnectionChange((int)evt.State); };
        Conductor.Instance.OnConnectionHealthStats += (RTCPeerConnectionHealthStats evt) => { this.OnNetworkLatencyChange(evt.RTT); };
#endif
    }
    
    /// <summary>
    /// Executes a chunk of work and emitted an <see cref="Error"/> event on failure
    /// </summary>
    /// <param name="action">chunk of work</param>
    private void ErrorOnFailure(Action action)
    {
        if (action == null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (this.Error != null)
            {
                this.Error(ex);
            }
        }
    }

    #region Dll Imports

    private static class Native
    {
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void CreateMediaPlayback();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void ReleaseMediaPlayback();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void GetPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void LoadContent([MarshalAs(UnmanagedType.BStr)] string sourceURL);

#if UNITY_WSA && !UNITY_EDITOR

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void LoadMediaSource(IMediaSource IMediaSourceHandler);

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void LoadMediaStreamSource(MediaStreamSource IMediaSourceHandler);
#endif

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void Play();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void Pause();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
        [DllImport(PlaybackPluginName)]
#endif
        internal static extern void Stop();
    }

    #endregion

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }
            
            // TODO: set large fields to null.
            Native.ReleaseMediaPlayback();

            disposedValue = true;
        }
    }

    ~StreamingUnityClientPlugin()
    {
       // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
       Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region GenericDelegate generics

    /// <summary>
    /// Helper to generate delegate signatures with generics
    /// </summary>
    public abstract class GenericDelegate
    {
        public delegate void Handler();
        private GenericDelegate() { }
    }

    /// <summary>
    /// Helper to generate delegate signatures with generics
    /// </summary>
    public abstract class GenericDelegate<TArg0>
    {
        public delegate void Handler(TArg0 arg0);
        private GenericDelegate() { }
    }

    /// <summary>
    /// Helper to generate delegate signatures with generics
    /// </summary>
    public abstract class GenericDelegate<TArg0, TArg1>
    {
        public delegate void Handler(TArg0 arg0, TArg1 arg1);
        private GenericDelegate() { }
    }

    /// <summary>
    /// Helper to generate delegate signatures with generics
    /// </summary>
    public abstract class GenericDelegate<TArg0, TArg1, TArg2>
    {
        public delegate void Handler(TArg0 arg0, TArg1 arg1, TArg2 arg2);
        private GenericDelegate() { }
    }

    /// <summary>
    /// Helper to generate delegate signatures with generics
    /// </summary>
    public abstract class GenericDelegate<TArg0, TArg1, TArg2, TArg3>
    {
        public delegate void Handler(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3);
        private GenericDelegate() { }
    }

    #endregion
}
