using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SuspicionSystem : MonoBehaviour
{
    [Header("Scene References")]
    public Image suspicionRing;       
    public TextMeshProUGUI statusText; 

    [Header("Configuration")]
    public Gradient alertGradient;    

    [Header("Debug Control (0 to 1)")]
    [Range(0, 1)] 
    public float suspicionLevel = 0;
    
    void Update()
    {
        if (suspicionRing == null || statusText == null) return;

        // 1. Encher o anel
        suspicionRing.fillAmount = suspicionLevel;

        // 2. Cor do Anel (Azul -> Vermelho)
        if (alertGradient != null)
        {
            suspicionRing.color = alertGradient.Evaluate(suspicionLevel);
        }

        if (suspicionLevel >= 0.4f)


        // 3. Mudar o Texto (? ou !)
        if (suspicionLevel >= 1f)
        {
            statusText.text = "!";
            statusText.color = Color.white; // <--- AQUI: Agora forçamos o Branco
        }
        else
        {
            statusText.text = "?";
            statusText.color = Color.white;
        }
    }
}