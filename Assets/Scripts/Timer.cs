using UnityEngine;
using TMPro;
using Photon.Pun;

public class Timer : MonoBehaviour
{
    // The server time when the game started (synced across all clients)
    private static double gameStartTime = -1;
    private static bool gameStarted = false;

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
        
        // Auto-start when the scene loads
        Invoke(nameof(TryAutoStart), 1.0f);
    }
    
    private void TryAutoStart()
    {
        if (!gameStarted && PhotonNetwork.IsConnectedAndReady)
        {
            Begin();
        }
        else if (!gameStarted)
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
        if (gameStarted) return;
        
        // Use Photon's synchronized server time
        gameStartTime = PhotonNetwork.Time;
        gameStarted = true;
        
        Debug.Log($"[Timer] Started at server time: {gameStartTime}");
    }

    void Update()
    {
        if (gameStarted && gameStartTime > 0)
        {
            UpdateTimerDisplay();
        }
    }

    void UpdateTimerDisplay()
    {
        if (timerText == null) return;

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
        if (!gameStarted || gameStartTime <= 0) return 0f;
        return (float)(PhotonNetwork.Time - gameStartTime);
    }
    
    /// <summary>
    /// Reset for new game
    /// </summary>
    public static void ResetTimer()
    {
        gameStarted = false;
        gameStartTime = -1;
    }
}
