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
    public TMP_InputField playerNameInput;

    public TMP_InputField roomNameInput;
    public ScrollRect roomsScrollRect;
    public Transform roomListContent;
    public GameObject roomItemPrefab;

    [Header("Player Name")]
    public bool autoCreatePlayerNameInput = true;
    public string playerNamePrefsKey = "player_name";

    [Header("Lobby Buttons")]
    public Button createRoomButton;
    public Button backToMainButton;
    public TMP_Text lobbyStatusText;

    [Header("Room UI")]
    public TMP_Text roomNameText;
    public TMP_Text player1Name;
    public TMP_Text player2Name;
    public Button startButton;
    public Button leaveRoomButton;

    private Dictionary<string, GameObject> roomListEntries = new Dictionary<string, GameObject>();

    private bool _isInLobby;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        AutoBindUiReferences();

        TryAutoCreatePlayerNameInput();
        InitializePlayerNickname();

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
        WireLobbyButtons();
        WireRoomButtons();
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

    private void WireLobbyButtons()
    {
        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveAllListeners();
            createRoomButton.onClick.AddListener(CreateRoom);
        }

        if (backToMainButton != null)
        {
            backToMainButton.onClick.RemoveAllListeners();
            backToMainButton.onClick.AddListener(OnBackToMainClicked);
        }
    }

    private void WireRoomButtons()
    {
        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveAllListeners();
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
        }
    }

    private void AutoBindUiReferences()
    {
        // Panels are required to auto-find reliably.
        if (mainMenuPanel != null)
        {
            startButtonMainMenu ??= FindButtonByNameContains(mainMenuPanel, "start");
            exitButton ??= FindButtonByNameContains(mainMenuPanel, "exit");
            settingsButton ??= FindButtonByNameContains(mainMenuPanel, "setting");
        }

        if (lobbyMenuPanel != null)
        {
            playerNameInput ??= FindInputFieldByNameContains(lobbyMenuPanel, "player") ?? FindInputFieldByNameContains(lobbyMenuPanel, "nick");

            // Room name input: prefer one that contains "room" in the name, otherwise fall back.
            roomNameInput ??= FindInputFieldByNameContains(lobbyMenuPanel, "room") ?? lobbyMenuPanel.GetComponentInChildren<TMP_InputField>(true);
            lobbyStatusText ??= FindTmpTextByNameContains(lobbyMenuPanel, "status") ?? lobbyMenuPanel.GetComponentInChildren<TMP_Text>(true);
            roomsScrollRect ??= lobbyMenuPanel.GetComponentInChildren<ScrollRect>(true);

            createRoomButton ??= FindButtonByNameContains(lobbyMenuPanel, "create");
            backToMainButton ??= FindButtonByNameContains(lobbyMenuPanel, "back");
        }

        if (roomsScrollRect != null)
        {
            roomListContent ??= roomsScrollRect.content;
        }

        if (roomMenuPanel != null)
        {
            roomNameText ??= FindTmpTextByNameContains(roomMenuPanel, "room") ?? roomMenuPanel.GetComponentInChildren<TMP_Text>(true);
            player1Name ??= FindTmpTextByNameContains(roomMenuPanel, "player1");
            player2Name ??= FindTmpTextByNameContains(roomMenuPanel, "player2");
            startButton ??= FindButtonByNameContains(roomMenuPanel, "start");
            // Prefer a dedicated Leave/Exit button; only fall back to "Back".
            leaveRoomButton ??= FindButtonByNameContains(roomMenuPanel, "leave")
                ?? FindButtonByNameContains(roomMenuPanel, "exit")
                ?? FindButtonByNameContains(roomMenuPanel, "back");
        }
    }

    private static Button FindButtonByNameContains(GameObject root, string nameContains)
    {
        if (root == null || string.IsNullOrWhiteSpace(nameContains))
        {
            return null;
        }

        nameContains = nameContains.ToLowerInvariant();
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button != null && button.name.ToLowerInvariant().Contains(nameContains))
            {
                return button;
            }
        }

        return null;
    }

    private static TMP_InputField FindInputFieldByNameContains(GameObject root, string nameContains)
    {
        if (root == null || string.IsNullOrWhiteSpace(nameContains))
        {
            return null;
        }

        nameContains = nameContains.ToLowerInvariant();
        var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
        foreach (var input in inputs)
        {
            if (input != null && input.name.ToLowerInvariant().Contains(nameContains))
            {
                return input;
            }
        }

        return null;
    }

    private void TryAutoCreatePlayerNameInput()
    {
        if (!autoCreatePlayerNameInput || playerNameInput != null || lobbyMenuPanel == null)
        {
            return;
        }

        // Create a basic TMP input field under the lobby panel.
        var resources = new TMP_DefaultControls.Resources();
        var go = TMP_DefaultControls.CreateInputField(resources);
        go.name = "PlayerNameInput";
        go.transform.SetParent(lobbyMenuPanel.transform, false);

        playerNameInput = go.GetComponent<TMP_InputField>();
        if (playerNameInput == null)
        {
            return;
        }

        playerNameInput.characterLimit = 16;
        playerNameInput.contentType = TMP_InputField.ContentType.Standard;

        // Set placeholder text if present.
        if (playerNameInput.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Player Name";
        }

        // Position it in a reasonable place (above the room name input if we can find it).
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(220f, 36f);

        if (roomNameInput != null)
        {
            var roomRt = roomNameInput.GetComponent<RectTransform>();
            if (roomRt != null)
            {
                rt.anchoredPosition = roomRt.anchoredPosition + new Vector2(0f, roomRt.sizeDelta.y + 12f);
                return;
            }
        }

        rt.anchoredPosition = new Vector2(0f, 24f);
    }

    private void InitializePlayerNickname()
    {
        string savedName = PlayerPrefs.GetString(playerNamePrefsKey, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(savedName))
        {
            savedName = $"Player{Random.Range(1000, 9999)}";
            PlayerPrefs.SetString(playerNamePrefsKey, savedName);
            PlayerPrefs.Save();
        }

        PhotonNetwork.NickName = savedName;

        if (playerNameInput != null)
        {
            playerNameInput.onEndEdit.RemoveAllListeners();
            playerNameInput.SetTextWithoutNotify(savedName);
            playerNameInput.onEndEdit.AddListener(OnPlayerNameSubmitted);
        }
    }

    private void OnPlayerNameSubmitted(string value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            // Revert to current nickname if input is cleared.
            if (playerNameInput != null)
            {
                playerNameInput.SetTextWithoutNotify(PhotonNetwork.NickName);
            }
            return;
        }

        if (trimmed.Length > 16)
        {
            trimmed = trimmed.Substring(0, 16);
        }

        PhotonNetwork.NickName = trimmed;
        PlayerPrefs.SetString(playerNamePrefsKey, trimmed);
        PlayerPrefs.Save();
    }

    private static TMP_Text FindTmpTextByNameContains(GameObject root, string nameContains)
    {
        if (root == null || string.IsNullOrWhiteSpace(nameContains))
        {
            return null;
        }

        nameContains = nameContains.ToLowerInvariant();
        var labels = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var label in labels)
        {
            if (label != null && label.name.ToLowerInvariant().Contains(nameContains))
            {
                return label;
            }
        }

        return null;
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

        // UI can change while panels are inactive; refresh bindings each time.
        AutoBindUiReferences();
        WireMainMenuButtons();
        WireLobbyButtons();
        WireRoomButtons();
    }

    public void OnStartClicked() => ShowPanel(lobbyMenuPanel);
    public void OnBackToMainClicked() => ShowPanel(mainMenuPanel);
    public void OnExitClicked() => Application.Quit();

    public void OnLeaveRoomClicked()
    {
        if (PhotonNetwork.InRoom)
        {
            SetLobbyStatus("Leaving room...");
            PhotonNetwork.LeaveRoom();
            // The UI will switch in OnLeftRoom.
            return;
        }

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
        roomNameText.text = PhotonNetwork.CurrentRoom.Name;
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
                    button.onClick.AddListener(() => TryJoinRoom(roomName));
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

    private void TryJoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            SetLobbyStatus("Not connected to server!");
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            SetLobbyStatus("Not in lobby yet. Please wait...");
            return;
        }

        if (PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.JoinedLobby)
        {
            SetLobbyStatus("Please wait, connecting to lobby...");
            return;
        }

        Debug.Log($"Attempting to join room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
        SetLobbyStatus($"Joining {roomName}...");
    }

    // --- PLAYER LIST LOGIC ---
    public override void OnPlayerEnteredRoom(Player newPlayer) => UpdatePlayerList();
    public override void OnPlayerLeftRoom(Player otherPlayer) => UpdatePlayerList();

    private void UpdatePlayerList()
    {
        Player[] players = PhotonNetwork.PlayerList;
        player1Name.text = players.Length >= 1 ? players[0].NickName : "Waiting...";
        player2Name.text = players.Length >= 2 ? players[1].NickName : "Waiting...";

        // Only the Master Client (host) can start, and only if room is full.
        bool canStart = PhotonNetwork.IsMasterClient && players.Length >= 1;

        if (startButton != null)
        {
            startButton.interactable = canStart;
            // "Only appear when can be clicked"
            startButton.gameObject.SetActive(canStart);
        }
    }

    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("MapScene"); // Make sure scene is in Build Settings
        }
    }
}