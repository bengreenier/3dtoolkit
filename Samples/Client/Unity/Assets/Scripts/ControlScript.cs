using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Toolkit.ThreeD;

#if !UNITY_EDITOR
using Org.WebRtc;
using WebRtcWrapper;
using PeerConnectionClient.Signalling;
using Windows.Media.Playback;
using Windows.Media.Core;
#endif

public class ControlScript : MonoBehaviour
{
    public uint TextureWidth = 2560;
    public uint TextureHeight = 720;
    public uint FrameRate = 30;

    // Heartbeat interval in ms (-1 will disable)
    public int HeartbeatInputText = 5000;
    public Text StatusText;
    public Text MessageText;
    public InputField ServerInputTextField;
    public InputField PeerInputTextField;
    public InputField MessageInputField;
    public Transform VirtualCamera;
    
    public RawImage LeftCanvas;
    public RawImage RightCanvas;

    public Camera LeftCamera;
    public Camera RightCamera;

	[Tooltip("A code provider uri of an oauth24d provider")]
	public string CredentialCodeUri;

	[Tooltip("A polling uri of an oauth24d provider")]
	public string CredentialPollUri;

	[Tooltip("A uri to a service that provides temporary turn creds")]
	public string TemporaryTurnCredentialUri;

	[Tooltip("A flag that indicates if we require authentication before communicating with the TemporaryTurnCredentialUri")]
	public bool AuthenticateTemporaryTurnCredentials = true;

#if !UNITY_EDITOR
    private Matrix4x4 leftViewProjection;
    private Matrix4x4 rightViewProjection;
    private string cameraTransformMsg;
    private WebRtcControl _webRtcControl;
    
    private bool enabledStereo = false;
#endif

    #region Graphics Low-Level Plugin DLL Setup
#if !UNITY_EDITOR
    private MediaVideoTrack _peerVideoTrack;
#endif
    #endregion

    void Awake()
    {
    }
    
    void Start()
    {
#if !UNITY_EDITOR
        _webRtcControl = new WebRtcControl();
        _webRtcControl.OnInitialized += WebRtcControlOnInitialized;
        _webRtcControl.OnPeerMessageDataReceived += WebRtcControlOnPeerMessageDataReceived;
        _webRtcControl.OnStatusMessageUpdate += WebRtcControlOnStatusMessageUpdate;

        Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;

		// if we have a temp turn uri, configure the turn client
		if (!string.IsNullOrEmpty(TemporaryTurnCredentialUri))
		{
			Conductor.Instance.ConfigureTemporaryTurn(TemporaryTurnCredentialUri);
			Conductor.Instance.TurnClient.CredentialsRetrieved += Conductor_CredentialsRetrieved;
		}

		// if we have a cred code and a cred poll
		if (!string.IsNullOrEmpty(CredentialCodeUri) && !string.IsNullOrEmpty(CredentialPollUri))
		{
			// configure auth (and start auth)
			Conductor.Instance.ConfigureAuth(CredentialCodeUri, CredentialPollUri);
			Conductor.Instance.AuthClient.CodeComplete += Conductor_CodeCompleted;
			Conductor.Instance.AuthClient.Authenticate();
		}
		// else if we require auth (and don't have it) and have a temp cred
		else if (AuthenticateTemporaryTurnCredentials && !string.IsNullOrEmpty(TemporaryTurnCredentialUri))
		{
			// throw
			throw new Exception("Authentication isn't configured, and temporary turn client requires auth");
		}
		// else we just have temp cred
		else
		{
			// so we request creds
			Conductor.Instance.TurnClient.RequestCredentials();
		}
		
		if (Conductor.Instance.TurnClient != null)
		{
			Conductor.Instance.TurnClient.CredentialsRetrieved += (TemporaryTurnClient.TurnCredentials data) =>
			{
				Debug.Log("turn creds got " + data.http_status);
			};
		}

		if (Conductor.Instance.AuthClient != null)
		{
			Conductor.Instance.AuthClient.CodeComplete += (OAuth24DClient.CodeCompletionData data) =>
			{
				Debug.Log("code got " + data.http_status);
			};

			Conductor.Instance.AuthClient.AuthenticationComplete += (OAuth24DClient.AuthCompletionData data) =>
			{
				Debug.Log("poll got " + data.http_status);
			};
		}

        _webRtcControl.Initialize();
#endif
	}

#if !UNITY_EDITOR
    private void Conductor_OnAddRemoteStream(MediaStreamEvent evt)
    {
        System.Diagnostics.Debug.WriteLine("Conductor_OnAddRemoteStream()");
        _peerVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
        if (_peerVideoTrack != null)
        {
            System.Diagnostics.Debug.WriteLine(
                "Conductor_OnAddRemoteStream() - GetVideoTracks: {0}",
                evt.Stream.GetVideoTracks().Count);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Conductor_OnAddRemoteStream() - peerVideoTrack NULL");
        }
        _webRtcControl.IsReadyToDisconnect = true;
    }

	private void Conductor_CodeCompleted(OAuth24DClient.CodeCompletionData data)
	{
		if (data.http_status == 200)
		{
			// show the user data.device_code
			// show the user data.verification_url
			// direct the user to enter device_code @ verification_url in a browser
			EnqueueAction(() => UpdateStatusText(string.Format("Visit {0} - Enter '{1}'\n", data.verification_url, data.user_code)));
		}
	}

	private void Conductor_CredentialsRetrieved(TemporaryTurnClient.TurnCredentials data)
	{
		if (data.http_status == 200)
		{
			EnqueueAction(() => UpdateStatusText("Got temporary turn creds\n"));
			
			// in this case, we don't autoconnect, we wait for the user to press connect
			EnqueueAction(() => UpdateStatusText("Ready to connect\n"));
		}
	}

#endif

	private void WebRtcControlOnInitialized()
    {
        EnqueueAction(OnInitialized);

		// if we aren't using auth, we can auto connect
		if (string.IsNullOrEmpty(CredentialCodeUri) && string.IsNullOrEmpty(CredentialPollUri))
		{
			ConnectToServer();
		}
    }

    private void OnInitialized()
    {
#if !UNITY_EDITOR
        _webRtcControl.SelectedVideoCodec = _webRtcControl.VideoCodecs.FirstOrDefault(x => x.Name.Contains("H264"));
#endif
        StatusText.text += "WebRTC Initialized\n";
    }

    private void WebRtcControlOnPeerMessageDataReceived(int peerId, string message)
    {
        EnqueueAction(() => UpdateMessageText(string.Format("{0}-{1}", peerId, message)));
    }
    
    private void WebRtcControlOnStatusMessageUpdate(string msg)
    {
        EnqueueAction(() => UpdateStatusText(string.Format("{0}\n", msg)));
    }

    private void UpdateMessageText(string msg)
    {
        MessageText.text += msg;
    }

    private void UpdateStatusText(string msg)
    {
        StatusText.text += msg;
    }

    public void ConnectToServer()
    {
#if !UNITY_EDITOR
        var signalhost = ServerInputTextField.text.Split(':');
        var host = string.Empty;
        var port = string.Empty;
        if (signalhost.Length > 1)
        {
            host = signalhost[0];
            port = signalhost[1];
        }
        else
        {
            host = signalhost[0];
            port = "8888";
        }
        
        _webRtcControl.ConnectToServer(PeerInputTextField.text, HeartbeatInputText);
#endif
    }

    public void DisconnectFromServer()
    {
#if !UNITY_EDITOR
        _webRtcControl.DisconnectFromServer();
#endif
    }

    public void ConnectToPeer()
    {    
#if !UNITY_EDITOR
        if (_webRtcControl.Peers.Count > 0)
        {
            _webRtcControl.SelectedPeer = _webRtcControl.Peers[0];
            _webRtcControl.ConnectToPeer();
        }
#endif
    }

    public void DisconnectFromPeer()
    {
#if !UNITY_EDITOR
        _webRtcControl.DisconnectFromPeer();
#endif
    }

    public void SendMessageToPeer()
    {
#if !UNITY_EDITOR
        _webRtcControl.SendPeerMessageData(MessageInputField.text);
#endif
    }

    public void ClearStatusText()
    {
        StatusText.text = string.Empty;
    }

    public void ClearMessageText()
    {
        MessageText.text = string.Empty;
    }

    public void EnqueueAction(Action action)
    {
		UIThreadSingleton.Dispatch(action);
    }

    private void OnEnable()
    {
        Plugin.CreateMediaPlayback();
        GetPlaybackTextureFromPlugin();
    }

    private void OnDisable()
    {
        Plugin.ReleaseMediaPlayback();
    }

    void Update()
    {
#if !UNITY_EDITOR
        leftViewProjection = LeftCamera.cullingMatrix;
        rightViewProjection = RightCamera.cullingMatrix;

        // Builds the camera transform message.
        var leftCameraTransform = "";
        var rightCameraTransform = "";
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                leftCameraTransform += leftViewProjection[i, j] + ",";
                rightCameraTransform += rightViewProjection[i, j];
                if (i != 3 || j != 3)
                {
                    rightCameraTransform += ",";
                }
            }
        }

        var cameraTransformBody = leftCameraTransform + rightCameraTransform;
        cameraTransformMsg =
           "{" +
           "  \"type\":\"camera-transform-stereo\"," +
           "  \"body\":\"" + cameraTransformBody + "\"" +
           "}";

        _webRtcControl.SendPeerDataChannelMessage(cameraTransformMsg);

        if (!enabledStereo && _peerVideoTrack != null)
        {
            // Enables the stereo output mode.
            var msg = "{" +
            "  \"type\":\"stereo-rendering\"," +
            "  \"body\":\"1\"" +
            "}";

            if(_webRtcControl.SendPeerDataChannelMessage(msg))
            {
                // Start the stream when the server is in stero mode to avoid corrupt frames at startup.
                var source = Media.CreateMedia().CreateMediaStreamSource(_peerVideoTrack, FrameRate, "media");
                Plugin.LoadMediaStreamSource((MediaStreamSource)source);
                Plugin.Play();
                enabledStereo = true;
            }
        }
#endif
    }

    private void GetPlaybackTextureFromPlugin()
    {
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetPrimaryTexture(TextureWidth, TextureHeight, out nativeTex);
        var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)TextureWidth, (int)TextureHeight, TextureFormat.BGRA32, false, false, nativeTex);

        LeftCanvas.texture = primaryPlaybackTexture;
        RightCanvas.texture = primaryPlaybackTexture;
    }

	private static class Plugin
    {
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateMediaPlayback")]
        internal static extern void CreateMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseMediaPlayback")]
        internal static extern void ReleaseMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetPrimaryTexture")]
        internal static extern void GetPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadContent")]
        internal static extern void LoadContent([MarshalAs(UnmanagedType.BStr)] string sourceURL);
#if !UNITY_EDITOR

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadMediaSource")]
        internal static extern void LoadMediaSource(IMediaSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadMediaStreamSource")]
        internal static extern void LoadMediaStreamSource(MediaStreamSource IMediaSourceHandler);
#endif
    
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Play")]
        internal static extern void Play();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Pause")]
        internal static extern void Pause();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Stop")]
        internal static extern void Stop();
	}
}
