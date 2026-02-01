using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PickupPromptUI : MonoBehaviour
{
    public static PickupPromptUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    
    [Header("Settings")]
    [SerializeField] private string defaultPrompt = "Press E to pick up";

    private void Awake()
    {
        Instance = this;
        HidePrompt();
    }

    public void ShowPrompt(string itemName = null)
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
        }

        if (promptText != null)
        {
            if (!string.IsNullOrEmpty(itemName))
            {
                promptText.text = $"Press E to pick up {itemName}";
            }
            else
            {
                promptText.text = defaultPrompt;
            }
        }
    }

    public void HidePrompt()
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }
}
