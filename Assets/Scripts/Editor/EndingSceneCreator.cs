using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public class EndingSceneCreator : EditorWindow
{
    [MenuItem("Tools/Create Ending Scene UI")]
    public static void CreateEndingSceneUI()
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
        SetFullStretch(bgRect);

        // Create Lore Panel
        GameObject lorePanel = new GameObject("LorePanel");
        lorePanel.transform.SetParent(canvasObj.transform, false);
        CanvasGroup loreGroup = lorePanel.AddComponent<CanvasGroup>();
        RectTransform loreRect = lorePanel.AddComponent<RectTransform>();
        SetFullStretch(loreRect);

        // Create Lore Text
        GameObject loreTextObj = new GameObject("LoreText");
        loreTextObj.transform.SetParent(lorePanel.transform, false);
        TextMeshProUGUI loreText = loreTextObj.AddComponent<TextMeshProUGUI>();
        loreText.text = "";
        loreText.fontSize = 36;
        loreText.alignment = TextAlignmentOptions.Center;
        loreText.color = Color.white;
        RectTransform loreTextRect = loreTextObj.GetComponent<RectTransform>();
        loreTextRect.anchorMin = new Vector2(0.1f, 0.2f);
        loreTextRect.anchorMax = new Vector2(0.9f, 0.8f);
        loreTextRect.offsetMin = Vector2.zero;
        loreTextRect.offsetMax = Vector2.zero;

        // Create Credits Panel
        GameObject creditsPanel = new GameObject("CreditsPanel");
        creditsPanel.transform.SetParent(canvasObj.transform, false);
        CanvasGroup creditsGroup = creditsPanel.AddComponent<CanvasGroup>();
        creditsGroup.alpha = 0f;
        RectTransform creditsRect = creditsPanel.AddComponent<RectTransform>();
        SetFullStretch(creditsRect);

        // Create Credits Container (scrollable)
        GameObject creditsContainer = new GameObject("CreditsContainer");
        creditsContainer.transform.SetParent(creditsPanel.transform, false);
        RectTransform containerRect = creditsContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0, -300);
        containerRect.sizeDelta = new Vector2(1200, 2000);

        // Create Credits Text
        GameObject creditsTextObj = new GameObject("CreditsText");
        creditsTextObj.transform.SetParent(creditsContainer.transform, false);
        TextMeshProUGUI creditsText = creditsTextObj.AddComponent<TextMeshProUGUI>();
        creditsText.text = "CRÉDITOS";
        creditsText.fontSize = 32;
        creditsText.alignment = TextAlignmentOptions.Top;
        creditsText.color = Color.white;
        RectTransform creditsTextRect = creditsTextObj.GetComponent<RectTransform>();
        creditsTextRect.anchorMin = Vector2.zero;
        creditsTextRect.anchorMax = Vector2.one;
        creditsTextRect.offsetMin = Vector2.zero;
        creditsTextRect.offsetMax = Vector2.zero;

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
        SetFullStretch(fadeRect);

        // Create EndingSceneManager and assign references
        GameObject managerObj = new GameObject("EndingSceneManager");
        EndingSceneManager manager = managerObj.AddComponent<EndingSceneManager>();

        // Assign references using SerializedObject
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("loreCanvasGroup").objectReferenceValue = loreGroup;
        so.FindProperty("creditsCanvasGroup").objectReferenceValue = creditsGroup;
        so.FindProperty("loreText").objectReferenceValue = loreText;
        so.FindProperty("creditsText").objectReferenceValue = creditsText;
        so.FindProperty("creditsContainer").objectReferenceValue = containerRect;
        so.FindProperty("fadeOverlay").objectReferenceValue = fadeGroup;
        so.FindProperty("blackBackground").objectReferenceValue = bgImage;
        so.FindProperty("fadeOverlayImage").objectReferenceValue = fadeImage;
        so.ApplyModifiedProperties();

        // Select the manager so user can see it
        Selection.activeGameObject = managerObj;

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("✅ Ending Scene UI created successfully! Check the EndingSceneManager to customize the text.");
        EditorUtility.DisplayDialog("Success!", "Ending Scene UI created!\n\nSelect 'EndingSceneManager' in the Hierarchy to customize the lore and credits text.\n\nDon't forget to save the scene (Ctrl+S)!", "OK");
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
