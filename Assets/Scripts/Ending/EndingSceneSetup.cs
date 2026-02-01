using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to setup the EndingScene UI structure in the Unity Editor.
/// Attach this to an empty GameObject and click the context menu to setup.
/// After setup, you can remove this script.
/// </summary>
public class EndingSceneSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Setup Ending Scene")]
    public void SetupEndingScene()
    {
        // Create Main Canvas
        GameObject canvasObj = new GameObject("EndingCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Black Background
        GameObject bgObj = new GameObject("BlackBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Create Lore Panel
        GameObject lorePanel = new GameObject("LorePanel");
        lorePanel.transform.SetParent(canvasObj.transform, false);
        CanvasGroup loreGroup = lorePanel.AddComponent<CanvasGroup>();
        RectTransform loreRect = lorePanel.GetComponent<RectTransform>();
        loreRect.anchorMin = Vector2.zero;
        loreRect.anchorMax = Vector2.one;
        loreRect.sizeDelta = Vector2.zero;

        // Create Lore Text
        GameObject loreTextObj = new GameObject("LoreText");
        loreTextObj.transform.SetParent(lorePanel.transform, false);
        TMP_Text loreText = loreTextObj.AddComponent<TextMeshProUGUI>();
        loreText.text = "";
        loreText.fontSize = 36;
        loreText.alignment = TextAlignmentOptions.Center;
        loreText.color = Color.white;
        RectTransform loreTextRect = loreTextObj.GetComponent<RectTransform>();
        loreTextRect.anchorMin = new Vector2(0.1f, 0.1f);
        loreTextRect.anchorMax = new Vector2(0.9f, 0.9f);
        loreTextRect.sizeDelta = Vector2.zero;

        // Create Credits Panel
        GameObject creditsPanel = new GameObject("CreditsPanel");
        creditsPanel.transform.SetParent(canvasObj.transform, false);
        CanvasGroup creditsGroup = creditsPanel.AddComponent<CanvasGroup>();
        creditsGroup.alpha = 0f;
        RectTransform creditsRect = creditsPanel.GetComponent<RectTransform>();
        creditsRect.anchorMin = Vector2.zero;
        creditsRect.anchorMax = Vector2.one;
        creditsRect.sizeDelta = Vector2.zero;

        // Create Credits Container (scrollable)
        GameObject creditsContainer = new GameObject("CreditsContainer");
        creditsContainer.transform.SetParent(creditsPanel.transform, false);
        RectTransform containerRect = creditsContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0, -200);
        containerRect.sizeDelta = new Vector2(1000, 2000);

        // Create Credits Text
        GameObject creditsTextObj = new GameObject("CreditsText");
        creditsTextObj.transform.SetParent(creditsContainer.transform, false);
        TMP_Text creditsText = creditsTextObj.AddComponent<TextMeshProUGUI>();
        creditsText.text = "CRÉDITOS";
        creditsText.fontSize = 32;
        creditsText.alignment = TextAlignmentOptions.Top;
        creditsText.color = Color.white;
        RectTransform creditsTextRect = creditsTextObj.GetComponent<RectTransform>();
        creditsTextRect.anchorMin = Vector2.zero;
        creditsTextRect.anchorMax = Vector2.one;
        creditsTextRect.sizeDelta = Vector2.zero;

        // Create Fade Overlay
        GameObject fadeObj = new GameObject("FadeOverlay");
        fadeObj.transform.SetParent(canvasObj.transform, false);
        Image fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        CanvasGroup fadeGroup = fadeObj.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.sizeDelta = Vector2.zero;

        // Create EndingSceneManager
        GameObject managerObj = new GameObject("EndingSceneManager");
        EndingSceneManager manager = managerObj.AddComponent<EndingSceneManager>();

        // Use SerializedObject to set references
        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("loreCanvasGroup").objectReferenceValue = loreGroup;
        serializedManager.FindProperty("creditsCanvasGroup").objectReferenceValue = creditsGroup;
        serializedManager.FindProperty("loreText").objectReferenceValue = loreText;
        serializedManager.FindProperty("creditsText").objectReferenceValue = creditsText;
        serializedManager.FindProperty("creditsContainer").objectReferenceValue = containerRect;
        serializedManager.FindProperty("fadeOverlay").objectReferenceValue = fadeGroup;
        serializedManager.ApplyModifiedProperties();

        Debug.Log("Ending Scene setup complete! You can now delete the EndingSceneSetup component.");
        
        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
#endif
}
