using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    // Removing 'static' fixes the "multiple players = triple speed" bug
    private float timer;
    private bool timeStarted = false;

    [SerializeField] private TextMeshProUGUI timerText;

    void Start()
    {
        // Automatically find the TMP component if you tagged the UI object "TIMER"
        if (timerText == null)
        {
            GameObject timerObj = GameObject.FindWithTag("TIMER");
            if (timerObj != null)
            {
                timerText = timerObj.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    // Call this from your other scripts to start the clock
    public void Begin()
    {
        timer = 0;
        timeStarted = true;
    }

    void Update()
    {
        if (timeStarted)
        {
            timer += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }

    void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timer / 60);
        int seconds = Mathf.FloorToInt(timer % 60);

        // Standard string formatting: "00:00"
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
