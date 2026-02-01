using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class Timer : MonoBehaviour
{
    private const string GAME_START_TIME_KEY = "GameStartTime";
    
    [SerializeField] private TextMeshProUGUI timerText;

    void Start()
    {
        // Automatically find the TMP component if tagged "TIMER"
        if (timerText == null)
        {
            GameObject timerObj = GameObject.FindWithTag("TIMER");
            if (timerObj != null)
            {
                timerText = timerObj.GetComponent<TextMeshProUGUI>();
                Debug.Log($"[Timer] Found timer text: {timerText.name}");
            }
            else
            {
                Debug.LogWarning("[Timer] No object with tag 'TIMER' found!");
            }
        }
        
        // Set initial display
        if (timerText != null)
        {
            timerText.text = "00:00";
        }
        
        // Check if timer already started (for late joiners)
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(GAME_START_TIME_KEY))
            {
                Debug.Log($"[Timer] Late joiner - timer already running");
            }
        }
        
        // Auto-start when the scene loads
        Invoke(nameof(TryAutoStart), 1.0f);
    }
    
    private void TryAutoStart()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            // Check if game already started (from room properties)
            if (!IsGameStarted())
            {
                Begin();
            }
        }
        else
        {
            // Retry if network not ready yet
            Invoke(nameof(TryAutoStart), 0.5f);
        }
    }

    /// <summary>
    /// Call this to start the timer. Uses Photon server time for sync.
    /// </summary>
    public void Begin()
    {
        if (IsGameStarted()) return;
        
        // Only master client sets the start time
        if (PhotonNetwork.IsMasterClient)
        {
            // Use Photon's synchronized server time and store in room properties
            double startTime = PhotonNetwork.Time;
            
            Hashtable props = new Hashtable();
            props[GAME_START_TIME_KEY] = startTime;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            
            Debug.Log($"[Timer] Master started timer at server time: {startTime}");
        }
    }
    
    private bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null) return false;
        return PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(GAME_START_TIME_KEY);
    }
    
    private double GetGameStartTime()
    {
        if (PhotonNetwork.CurrentRoom == null) return -1;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(GAME_START_TIME_KEY)) return -1;
        
        object startTimeObj = PhotonNetwork.CurrentRoom.CustomProperties[GAME_START_TIME_KEY];
        return startTimeObj != null ? (double)startTimeObj : -1;
    }

    void Update()
    {
        if (IsGameStarted())
        {
            UpdateTimerDisplay();
        }
    }

    void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        double gameStartTime = GetGameStartTime();
        if (gameStartTime <= 0) return;

        // Calculate elapsed time using Photon's synchronized server time
        double elapsedTime = PhotonNetwork.Time - gameStartTime;
        
        // Safety check for negative time
        if (elapsedTime < 0) elapsedTime = 0;

        int minutes = Mathf.FloorToInt((float)elapsedTime / 60);
        int seconds = Mathf.FloorToInt((float)elapsedTime % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    
    /// <summary>
    /// Get current elapsed time in seconds
    /// </summary>
    public float GetElapsedTime()
    {
        double gameStartTime = GetGameStartTime();
        if (gameStartTime <= 0) return 0f;
        return (float)(PhotonNetwork.Time - gameStartTime);
    }
    
    /// <summary>
    /// Reset for new game
    /// </summary>
    public static void ResetTimer()
    {
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            Hashtable props = new Hashtable();
            props[GAME_START_TIME_KEY] = null;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log("[Timer] Reset by master client");
        }
    }
}
