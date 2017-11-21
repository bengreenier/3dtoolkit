using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class WebRTCStatus : MonoBehaviour
{
    public enum ConnectionStatus
    {
        Closed,
        Connecting,
        Connected
    }

    public enum IceType
    {
        Unknown,
        Local,
        Stun,
        Turn
    }
    
    private ConnectionStatus connStatus = ConnectionStatus.Closed;
    private string peerName = "";
    private IceType iceType = IceType.Unknown;
    private string iceServer = "";
    private int networkLatency = 0;

    public void SetConnectionStatus(ConnectionStatus connStatus)
    {
        this.connStatus = connStatus;

        Redraw();
    }

    public void SetPeerName(string peerName)
    {
        this.peerName = peerName;

        Redraw();
    }

    public void SetIceInfo(IceType iceType, string iceServer)
    {
        this.iceType = iceType;
        this.iceServer = iceServer;

        Redraw();
    }

    public void SetNetworkLatency(int networkLatency)
    {
        this.networkLatency = networkLatency;

        Redraw();
    }

    private void Redraw()
    {
        this.GetComponent<Text>().text = string.Format("{0} | {1} | {2} | {3} | {4}ms",
            this.connStatus,
            this.peerName,
            this.iceType,
            this.iceServer,
            this.networkLatency);
    }

    private void Start()
    {
        Redraw();
    }
}