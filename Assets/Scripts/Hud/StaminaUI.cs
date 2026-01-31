using UnityEngine;
using UnityEngine.UI;

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

    private void Start()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("PlayerController not found! StaminaUI disabled.");
                enabled = false;
                return;
            }
        }

        if (staminaFill == null)
        {
            Debug.LogError("Stamina Fill Image not assigned! StaminaUI disabled.");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
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
