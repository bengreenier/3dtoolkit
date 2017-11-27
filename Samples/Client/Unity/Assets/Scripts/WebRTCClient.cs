using Microsoft.Toolkit.ThreeD;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebRTC client behavior that enables 3dtoolkit webrtc
/// </summary>
/// <remarks>
/// Adding this component requires an <c>nvEncConfig.json</c> and <c>webrtcConfig.json</c> in the run directory
/// </remarks>
public class WebRTCClient : MonoBehaviour
{
    /// <summary>
    /// The left eye camera
    /// </summary>
    /// <remarks>
    /// This camera is always needed, even if running in mono
    /// </remarks>
    [Tooltip("The left eye camera, or the only camera in a mono setup")]
    public Camera LeftEye;

    /// <summary>
    /// The right eye camera
    /// </summary>
    /// <remarks>
    /// This camera is only needed if running in stereo
    /// </remarks>
    [Tooltip("The right eye camera, only used in a stereo setup")]
    public Camera RightEye;

    /// <summary>
    /// The texture to draw the WebRTC stream onto
    /// </summary>
    [Tooltip("The texture to draw the WebRTC stream onto")]
    public RawImage Texture;

    /// <summary>
    /// The optional status bar to which we'll notify of underlying events
    /// </summary>
    [Tooltip("Optional status bar to which we'll notify of underlying events")]
    public Status.StatusBar Status = null;

    /// <summary>
    /// Indicates if we should use stereo
    /// </summary>
    [Tooltip("Flag indicating if we should use stereo rendering")]
    public bool ShouldUseStereo = true;

    /// <summary>
    /// Indicates the current rendering approach
    /// </summary>
    /// <remarks>
    /// This is left public to improve quick editor runtime inspection
    /// </remarks>
    [Tooltip("Flag indicating if we are currently rendering in stereo")]
    public bool IsStereo = false;

    /// <summary>
    /// Should we load the native plugin in the editor?
    /// </summary>
    /// <remarks>
    /// This requires webrtcConfig.json and nvEncConfig.json to exist in the unity
    /// application directory (where Unity.exe) lives, and requires a native plugin
    /// for the architecture of the editor (x64 vs x86).
    /// </remarks>
    [Tooltip("Flag indicating if we should load the native plugin in the editor")]
    public bool UseEditorNativePlugin = false;

    /// <summary>
    /// Instance that represents the underlying native plugin that powers the webrtc experience
    /// </summary>
    public StreamingUnityClientPlugin Plugin = null;

    /// <summary>
    /// Internal flag used to indicate we are shutting down
    /// </summary>
    private bool isClosing = false;

    /// <summary>
    /// Internal tracking id for the peer we're trying to connect to for video/data
    /// </summary>
    /// <remarks>
    /// We use this to know who is connected on <see cref="StreamingUnityClientPlugin.AddStream"/>
    /// </remarks>
    private int? offerPeer = null;

    /// <summary>
    /// Internal tracking bool for indicating if the peer offer succeeded
    /// </summary>
    private bool offerSucceeded = false;

    /// <summary>
    /// A list of all the connected peers
    /// </summary>
    private Dictionary<int, string> peerList = new Dictionary<int, string>();

    /// <summary>
    /// Unity engine object Awake() hook
    /// </summary>
    private void Awake()
    {
        // ensure that we aren't stereo to start with (because stereo requires sending the server a message
        // so it is impossible to start in that state)
        this.IsStereo = false;

        // make sure that the render window continues to render when the game window does not have focus
        Application.runInBackground = true;

        // open the connection
        Open();
    }

    /// <summary>
    /// Unity engine object OnDestroy() hook
    /// </summary>
    private void OnDestroy()
    {
        // close the connection
        Close();
    }

    /// <summary>
    /// Unity engine object Update() hook
    /// </summary>
    private void Update()
    {
        // if the plugin isn't loaded, don't do anything
        if (this.Plugin == null || !this.Plugin.HasVideoTrack)
        {
            return;
        }
        
        var leftViewProjection = this.LeftEye.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left) * this.LeftEye.cameraToWorldMatrix;
        var rightViewProjection = this.RightEye != null ?
            this.RightEye.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right) * this.RightEye.cameraToWorldMatrix :
            this.LeftEye.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right) * this.LeftEye.cameraToWorldMatrix;

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
        var cameraTransformMsg =
           "{" +
           "  \"type\":\"camera-transform" + (this.IsStereo ? "-stereo" : "") + "\"," +
           "  \"body\":\"" + cameraTransformBody + "\"" +
           "}";

        this.Plugin.SendDataChannelMessage(cameraTransformMsg);

        if (this.ShouldUseStereo && !this.IsStereo)
        {
            // build the stereo enabling message
            var msg = "{" +
            "  \"type\":\"stereo-rendering\"," +
            "  \"body\":\"1\"" +
            "}";

            Debug.Log("sending stereo");

            // enable stereo on the server
            if (this.Plugin.SendDataChannelMessage(msg))
            {
                this.IsStereo = true;

                Debug.Log("sent stereo message");
            }
        }

        // start playing if we're not already playing
        if (!this.Plugin.IsPlaying)
        {
            // TODO(bengreenier): this should only happen 1x...
            Debug.Log("playing");

            this.Plugin.Play();
        }
        
    }

    /// <summary>
    /// Opens the webrtc server and gets things rolling
    /// </summary>
    public void Open()
    {
        if (Plugin != null)
        {
            Close();
        }
        
        // check if we're in the editor, and fail out if we aren't loading the plugin in editor
        if (Application.isEditor && !UseEditorNativePlugin)
        {
            return;
        }

        // Create the plugin
        Plugin = new StreamingUnityClientPlugin(this.Texture);

        Plugin.StatusMessageUpdate += (string status) =>
        {
            Debug.Log(status);
        };

        Plugin.SignIn += (string serverName) =>
        {
            // update status to the signaling server uri
            if (this.Status != null)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnConnectionStatusChange.Invoke(serverName);
                });
            }
        };

        Plugin.Disconnect += () =>
        {
            // update status to the signaling server uri
            if (this.Status != null && !this.offerPeer.HasValue)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnConnectionStatusChange.Invoke("Disconnected");
                });
            }
        };

        Plugin.PeerConnect += (int peerId, string peerName) =>
        {
            this.peerList.Add(peerId, peerName);
        };

        Plugin.PeerDisconnect += (int peerId) =>
        {
            this.peerList.Remove(peerId);
            this.offerPeer = null;
        };

        Plugin.MessageFromPeer += (int peerId, string msg) =>
        {
            if (msg.Contains("\"type\": \"offer\""))
            {
                this.offerPeer = peerId;
            }
        };

        // when we add a stream successfully, track that
        Plugin.AddStream += (string streamLabel) =>
        {
            offerSucceeded = true;

            // update status to whom we connected to
            if (this.Status != null && this.offerPeer.HasValue)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnConnectionStatusChange.Invoke(this.peerList[this.offerPeer.Value]);
                });
            }
        };

        // when we remove a stream, track that
        Plugin.RemoveStream += (string streamLabel) =>
        {
            offerSucceeded = false;

            // update status to the signaling server (no peer)
            if (this.Status != null && this.offerPeer.HasValue)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnConnectionStatusChange.Invoke(this.peerList[this.offerPeer.Value]);
                });
            }
        };

        Plugin.IceCandidate += (string iceCandidate) =>
        {
            // update status to the latest ice candidate
            if (this.Status != null)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnIceStatusChange.Invoke(iceCandidate);
                });
            }
        };

        Plugin.NetworkLatencyChange += (long rTT) =>
        {
            // update status to the latest rtt value
            if (this.Status != null && this.offerPeer.HasValue)
            {
                UIThreadSingleton.Dispatch(() =>
                {
                    this.Status.OnNetworkLatencyChange.Invoke(rTT + "ms");
                });
            }
        };
    }

    /// <summary>
    /// Closes the webrtc server and shuts things down
    /// </summary>
    public void Close()
    {
        if (!isClosing && Plugin != null)
        {
            Plugin.Dispose();
            Plugin = null;
        }
    }
}
