using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class StaminaUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image staminaFill;
    [SerializeField] private PlayerController playerController;

    [Header("Settings")]
    [SerializeField] private Color fullStaminaColor = Color.red;
    [SerializeField] private Color lowStaminaColor = new Color(0.5f, 0f, 0f);
    [SerializeField] private float lowStaminaThreshold = 0.3f;
    [SerializeField] private bool hideWhenFull = true;

    private bool searchingForPlayer = false;

    private void Start()
    {
        if (staminaFill == null)
        {
            Debug.LogError("StaminaUI: Stamina Fill Image not assigned!");
            enabled = false;
            return;
        }

        TryFindLocalPlayer();
        
        if (playerController == null)
        {
            searchingForPlayer = true;
            Debug.LogWarning("StaminaUI: Player not found yet, will keep searching...");
        }
    }

    private void TryFindLocalPlayer()
    {
        if (playerController != null) return;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        Debug.Log($"StaminaUI: Found {allPlayers.Length} PlayerControllers in scene");
        
        foreach (PlayerController player in allPlayers)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null)
            {
                Debug.Log($"StaminaUI: Checking player with PhotonView - IsMine: {pv.IsMine}");
                if (pv.IsMine)
                {
                    playerController = player;
                    searchingForPlayer = false;
                    Debug.Log("StaminaUI: Found local player controller!");
                    return;
                }
            }
        }

        // Single player mode (no Photon)
        if (!PhotonNetwork.IsConnected && allPlayers.Length > 0)
        {
            playerController = allPlayers[0];
            searchingForPlayer = false;
            Debug.Log("StaminaUI: Single player mode - using first player");
        }
    }

    private void Update()
    {
        // Keep searching for player if not found yet
        if (searchingForPlayer)
        {
            TryFindLocalPlayer();
        }

        if (playerController == null || staminaFill == null) return;

        float staminaPercentage = playerController.GetStaminaPercentage();
        
        if (hideWhenFull && staminaPercentage >= 1f)
        {
            staminaFill.enabled = false;
            return;
        }
        
        staminaFill.enabled = true;
        staminaFill.fillAmount = staminaPercentage;

        if (staminaPercentage <= lowStaminaThreshold)
        {
            staminaFill.color = Color.Lerp(lowStaminaColor, fullStaminaColor, staminaPercentage / lowStaminaThreshold);
        }
        else
        {
            staminaFill.color = fullStaminaColor;
        }
    }
}
