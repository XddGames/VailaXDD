using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class Menu : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyMenuPanel;
    public GameObject roomMenuPanel;

    [Header("Main Menu UI")]
    public Button startButtonMainMenu;
    public Button settingsButton;
    public Button exitButton;

    [Header("Lobby UI")]
    public TMP_InputField roomNameInput;
    public ScrollRect roomsScrollRect;
    public Transform roomListContent;
    public GameObject roomItemPrefab;

    [Header("Lobby Status (Optional)")]
    public TMP_Text lobbyStatusText;

    [Header("Room UI")]
    public TMP_Text roomNameText;
    public TMP_Text player1Name;
    public TMP_Text player2Name;
    public Button startButton;

    private Dictionary<string, GameObject> roomListEntries = new Dictionary<string, GameObject>();

    private bool _isInLobby;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        // If only the ScrollRect is assigned, use its Content.
        if (roomListContent == null && roomsScrollRect != null)
        {
            roomListContent = roomsScrollRect.content;
        }

        EnsureRoomListScrollSetup();
    }

    void Start()
    {
        ShowPanel(mainMenuPanel);
        WireMainMenuButtons();
        SetLobbyStatus("Connecting...");
        PhotonNetwork.ConnectUsingSettings();
    }

    private void WireMainMenuButtons()
    {
        if (startButtonMainMenu != null)
        {
            startButtonMainMenu.onClick.RemoveAllListeners();
            startButtonMainMenu.onClick.AddListener(OnStartClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }

        // Settings button is intentionally left for you to implement.
    }

    private void SetLobbyStatus(string message)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = message;
        }
    }

    private void EnsureRoomListScrollSetup()
    {
        if (roomListContent == null)
        {
            return;
        }

        // Ensure Content has a vertical layout + size fitter so the ScrollRect can scroll.
        var layout = roomListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = roomListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
        }

        var fitter = roomListContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = roomListContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // If ScrollRect exists but content wasn't wired, wire it.
        if (roomsScrollRect != null && roomsScrollRect.content == null)
        {
            roomsScrollRect.content = roomListContent as RectTransform;
        }
    }

    // --- PANEL NAVIGATION ---
    public void ShowPanel(GameObject panelToShow)
    {
        mainMenuPanel.SetActive(false);
        lobbyMenuPanel.SetActive(false);
        roomMenuPanel.SetActive(false);
        panelToShow.SetActive(true);
    }

    public void OnStartClicked() => ShowPanel(lobbyMenuPanel);
    public void OnBackToMainClicked() => ShowPanel(mainMenuPanel);
    public void OnExitClicked() => Application.Quit();

    public void OnLeaveRoomClicked()
    {
        PhotonNetwork.LeaveRoom();
        ShowPanel(lobbyMenuPanel);
    }

    // --- PHOTON LOGIC ---
    public override void OnConnectedToMaster()
    {
        SetLobbyStatus("Connected. Joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        _isInLobby = true;
        SetLobbyStatus("In lobby. Waiting for rooms...");
        ClearRoomListUI();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _isInLobby = false;
        SetLobbyStatus($"Disconnected: {cause}");
        ClearRoomListUI();
    }

    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameInput.text)) return;

        if (!PhotonNetwork.IsConnected)
        {
            SetLobbyStatus("Not connected yet.");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        PhotonNetwork.CreateRoom(roomNameInput.text, new RoomOptions { MaxPlayers = 2 });
        SetLobbyStatus("Creating room...");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetLobbyStatus($"Create failed: {message} ({returnCode})");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetLobbyStatus($"Join failed: {message} ({returnCode})");
    }

    public override void OnJoinedRoom()
    {
        _isInLobby = false;
        ShowPanel(roomMenuPanel);
        roomNameText.text = "Room: " + PhotonNetwork.CurrentRoom.Name;
        UpdatePlayerList();
    }

    public override void OnLeftRoom()
    {
        ShowPanel(lobbyMenuPanel);
        SetLobbyStatus("Left room.");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (roomListContent == null || roomItemPrefab == null)
        {
            return;
        }

        if (!_isInLobby)
        {
            // Room list updates are only useful when you're in the lobby.
            return;
        }

        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                if (roomListEntries.ContainsKey(info.Name))
                {
                    Destroy(roomListEntries[info.Name]);
                    roomListEntries.Remove(info.Name);
                }
                continue;
            }

            if (!roomListEntries.ContainsKey(info.Name))
            {
                GameObject entry = Instantiate(roomItemPrefab, roomListContent);
                var label = entry.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"{info.Name} ({info.PlayerCount}/{info.MaxPlayers})";
                }
                // Capture the room name in a local variable to avoid closure bugs.
                string roomName = info.Name;

                var button = entry.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => PhotonNetwork.JoinRoom(roomName));
                    button.interactable = info.IsOpen && info.IsVisible && info.PlayerCount < info.MaxPlayers;
                }
                roomListEntries.Add(info.Name, entry);
            }
            else
            {
                // Keep the label updated if the room name display is customized later.
                var existingEntry = roomListEntries[info.Name];
                var label = existingEntry.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"{info.Name} ({info.PlayerCount}/{info.MaxPlayers})";
                }

                var button = existingEntry.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = info.IsOpen && info.IsVisible && info.PlayerCount < info.MaxPlayers;
                }
            }
        }

        // A small hint to the user if lobby is empty.
        if (roomListEntries.Count == 0)
        {
            SetLobbyStatus("No rooms found.");
        }
    }

    private void ClearRoomListUI()
    {
        foreach (var kvp in roomListEntries)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        roomListEntries.Clear();
    }

    // --- PLAYER LIST LOGIC ---
    public override void OnPlayerEnteredRoom(Player newPlayer) => UpdatePlayerList();
    public override void OnPlayerLeftRoom(Player otherPlayer) => UpdatePlayerList();

    private void UpdatePlayerList()
    {
        Player[] players = PhotonNetwork.PlayerList;
        player1Name.text = players.Length >= 1 ? players[0].NickName : "Waiting...";
        player2Name.text = players.Length >= 2 ? players[1].NickName : "Waiting...";

        // Only the Master Client (host) can click start, and only if room is full
        startButton.interactable = PhotonNetwork.IsMasterClient && players.Length == 2;
    }

    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("GameScene"); // Make sure scene is in Build Settings
        }
    }
}