using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetWork : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        Debug.Log("Status: Not Connected. Press 'C' to Connect or 'D' to Disconnect.");
    }

    private void Update()
    {
        // Keyboard shortcuts for quick testing
        if (Input.GetKeyDown(KeyCode.C)) Connect();
        if (Input.GetKeyDown(KeyCode.D)) Disconnect();
    }

    public void Connect()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Connecting to Master...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.LogWarning("Already connected!");
        }
    }

    public void Disconnect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Disconnecting from Photon...");
            PhotonNetwork.Disconnect();
        }
    }

    // --- PUN Callbacks ---

    public override void OnConnectedToMaster()
    {
        Debug.Log("Successfully connected to the Photon Master Server.");
        // Automatically join a lobby to be ready for room creation
        PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from server. Reason: {cause}");
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Entered the Lobby. Ready to join/create rooms.");
    }
}