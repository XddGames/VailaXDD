using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

/// <summary>
/// Utility class to load the ending scene from anywhere in the game.
/// Attach to a persistent manager or call the static method directly.
/// </summary>
public class EndingSceneLoader : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string endingSceneName = "EndingScene";

    private static EndingSceneLoader instance;
    private static string customLore = null;
    private static string customCredits = null;

    public static EndingSceneLoader Instance => instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    /// <summary>
    /// Loads the ending scene. Call this when the game ends.
    /// </summary>
    public void LoadEndingScene()
    {
        LoadEndingSceneStatic(endingSceneName);
    }

    /// <summary>
    /// Static method to load ending scene from anywhere.
    /// </summary>
    /// <param name="sceneName">Name of the ending scene</param>
    public static void LoadEndingSceneStatic(string sceneName = "EndingScene")
    {
        // Disconnect from Photon if connected
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.Disconnect();
        }

        // Load the ending scene
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Load ending scene with custom lore and credits.
    /// </summary>
    public static void LoadEndingSceneWithContent(string lore, string credits, string sceneName = "EndingScene")
    {
        customLore = lore;
        customCredits = credits;

        LoadEndingSceneStatic(sceneName);
    }

    /// <summary>
    /// Get stored custom lore (call from EndingSceneManager).
    /// </summary>
    public static string GetCustomLore()
    {
        string lore = customLore;
        customLore = null; // Clear after reading
        return lore;
    }

    /// <summary>
    /// Get stored custom credits (call from EndingSceneManager).
    /// </summary>
    public static string GetCustomCredits()
    {
        string credits = customCredits;
        customCredits = null; // Clear after reading
        return credits;
    }

    /// <summary>
    /// Check if there's custom content waiting.
    /// </summary>
    public static bool HasCustomContent()
    {
        return !string.IsNullOrEmpty(customLore) || !string.IsNullOrEmpty(customCredits);
    }
}
