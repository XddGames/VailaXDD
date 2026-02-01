using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class PickupFlashlight : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private string itemName = "Flashlight";
    [SerializeField] private float pickupRange = 2.5f; // Distance-based detection

    [Header("Prompt Style")]
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 24;

    private bool playerInRange = false;
    private InventoryManager nearbyPlayer;
    private bool isPickedUp = false; // Prevent double pickup
    
    // Runtime generated UI
    private static GameObject promptCanvas;
    private static TextMeshProUGUI promptText;

    private void Start()
    {
    }

    private void Update()
    {
        if (isPickedUp)
        {
            return;
        }
        
        // Distance-based detection (more reliable than triggers)
        CheckPlayerDistance();

        if (playerInRange && nearbyPlayer != null)
        {
            if (Input.GetKeyDown(pickupKey))
            {
                isPickedUp = true;
                HidePrompt();
                nearbyPlayer.PickupFlashlight(gameObject);
            }
        }
    }

    private void CheckPlayerDistance()
    {
        if (isPickedUp) return;
        
        // Find all players and check distance
        InventoryManager[] allPlayers = FindObjectsOfType<InventoryManager>();
        
        
        InventoryManager closestPlayer = null;
        float closestDistance = pickupRange;

        foreach (InventoryManager player in allPlayers)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < closestDistance)
                {
                    // Check if player already has flashlight
                    bool hasFlashlight = player.HasItem(InventoryItem.ItemType.Flashlight);
                    if (!hasFlashlight)
                    {
                        closestPlayer = player;
                        closestDistance = distance;
                    }
                }
            }
        }

        // Player entered range
        if (closestPlayer != null && !playerInRange)
        {
            playerInRange = true;
            nearbyPlayer = closestPlayer;
            ShowPrompt($"Press E to pick up {itemName}");
        }
        // Player left range
        else if (closestPlayer == null && playerInRange)
        {
            playerInRange = false;
            nearbyPlayer = null;
            HidePrompt();
        }
    }

    private void ShowPrompt(string message)
    {
        // Create the prompt UI if it doesn't exist
        if (promptCanvas == null)
        {
            CreatePromptUI();
        }

        promptCanvas.SetActive(true);
        promptText.text = message;
    }

    private void HidePrompt()
    {
        if (promptCanvas != null)
        {
            promptCanvas.SetActive(false);
        }
    }

    private void CreatePromptUI()
    {
        // Create Canvas
        promptCanvas = new GameObject("PickupPromptCanvas");
        Canvas canvas = promptCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        CanvasScaler scaler = promptCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        promptCanvas.AddComponent<GraphicRaycaster>();

        // Create background panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(promptCanvas.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = backgroundColor;
        
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0);
        panelRect.anchorMax = new Vector2(0.5f, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = new Vector2(0, 100);
        panelRect.sizeDelta = new Vector2(400, 60);

        // Create text
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panel.transform, false);
        promptText = textObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "Press E to pick up";
        promptText.color = textColor;
        promptText.fontSize = fontSize;
        promptText.alignment = TextAlignmentOptions.Center;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        DontDestroyOnLoad(promptCanvas);
    }

    private void OnDestroy()
    {
        if (playerInRange)
        {
            HidePrompt();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
