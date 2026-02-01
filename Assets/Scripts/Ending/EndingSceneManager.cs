using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the ending scene sequence: displays lore text with typewriter effect,
/// then shows credits rolling up the screen.
/// </summary>
public class EndingSceneManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup loreCanvasGroup;
    [SerializeField] private CanvasGroup creditsCanvasGroup;
    [SerializeField] private TMP_Text loreText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private RectTransform creditsContainer;
    [SerializeField] private Image blackBackground;
    [SerializeField] private Image fadeOverlayImage;

    [Header("Lore Settings")]
    [SerializeField] private float typewriterSpeed = 0.05f;
    [SerializeField] private float loreDisplayDuration = 5f;
    [SerializeField] private float loreFadeOutDuration = 1.5f;

    [Header("Credits Settings")]
    [SerializeField] private float creditsFadeInDuration = 1f;
    [SerializeField] private float creditsScrollSpeed = 50f;
    [SerializeField] private float creditsScrollDistance = 1000f;

    [Header("Scene Transition")]
    [SerializeField] private string menuSceneName = "SampleScene";
    [SerializeField] private float endFadeOutDuration = 2f;
    [SerializeField] private CanvasGroup fadeOverlay;

    [Header("Lore Content")]
    [TextArea(5, 20)]
    [SerializeField] private string loreContent = @"E assim termina a nossa jornada...

Nas sombras do cemitério antigo, os segredos foram finalmente revelados.
As almas perdidas encontraram o seu descanso eterno.

Mas lembra-te... algumas histórias nunca terminam verdadeiramente.
Elas apenas esperam pelo próximo corajoso que ouse descobri-las.

Até à próxima vez, viajante...";

    [Header("Credits Content")]
    [TextArea(10, 30)]
    [SerializeField] private string creditsContent = @"<size=48><b>CRÉDITOS</b></size>


<size=36>Desenvolvimento</size>
Filipe Gonçalves
Gonçalo Araújo
Gonçalo Fernandes
Tiago Miranda
Gonçalo Sousa

<size=36>Agradecimentos Especiais</size>
À comunidade Unity
A todos os jogadores

<size=32><i>Obrigado por jogares!</i></size>

<size=24>© 2026 - Todos os direitos reservados</size>";

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip endingMusic;

    private bool isSkippable = false;
    private bool isSkipping = false;

    private void Start()
    {
        // Initialize UI states
        if (loreCanvasGroup != null) loreCanvasGroup.alpha = 1f;
        if (creditsCanvasGroup != null) creditsCanvasGroup.alpha = 0f;
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;

        if (loreText != null) loreText.text = "";
        if (creditsText != null) creditsText.text = creditsContent;

        // Play ending music if assigned
        if (musicSource != null && endingMusic != null)
        {
            musicSource.clip = endingMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        // Start the ending sequence
        StartCoroutine(EndingSequence());
    }

    private void Update()
    {
        // Allow skipping with any key after a brief delay
        if (isSkippable && !isSkipping)
        {
            if (Input.anyKeyDown)
            {
                isSkipping = true;
                StopAllCoroutines();
                StartCoroutine(SkipToEnd());
            }
        }
    }

    private IEnumerator EndingSequence()
    {
        // Wait a moment before starting
        yield return new WaitForSeconds(1f);

        // Enable skipping
        isSkippable = true;

        // Show lore with typewriter effect
        yield return StartCoroutine(TypewriterEffect(loreText, loreContent));

        // Wait for player to read
        yield return new WaitForSeconds(loreDisplayDuration);

        // Fade out lore
        yield return StartCoroutine(FadeCanvasGroup(loreCanvasGroup, 1f, 0f, loreFadeOutDuration));

        // Fade in credits
        yield return StartCoroutine(FadeCanvasGroup(creditsCanvasGroup, 0f, 1f, creditsFadeInDuration));

        // Scroll credits
        yield return StartCoroutine(ScrollCredits());

        // End sequence
        yield return StartCoroutine(EndSequence());
    }

    private IEnumerator TypewriterEffect(TMP_Text textComponent, string fullText)
    {
        if (textComponent == null) yield break;

        textComponent.text = "";

        foreach (char c in fullText)
        {
            if (isSkipping) yield break;

            textComponent.text += c;

            // Add slight pause for punctuation
            if (c == '.' || c == '!' || c == '?')
            {
                yield return new WaitForSeconds(typewriterSpeed * 5f);
            }
            else if (c == ',')
            {
                yield return new WaitForSeconds(typewriterSpeed * 2f);
            }
            else
            {
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            if (isSkipping) yield break;

            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private IEnumerator ScrollCredits()
    {
        if (creditsContainer == null) yield break;

        Vector2 startPos = creditsContainer.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, creditsScrollDistance);

        float scrollDuration = creditsScrollDistance / creditsScrollSpeed;
        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            if (isSkipping) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / scrollDuration;
            creditsContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        creditsContainer.anchoredPosition = endPos;

        // Wait a moment at the end
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator EndSequence()
    {
        // Fade to black
        if (fadeOverlay != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, endFadeOutDuration));
        }

        yield return new WaitForSeconds(1f);

        // Return to main menu
        ReturnToMenu();
    }

    private IEnumerator SkipToEnd()
    {
        // Quick fade to black
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, 0.5f));
        }

        yield return new WaitForSeconds(0.5f);

        ReturnToMenu();
    }

    private void ReturnToMenu()
    {
        // If using Photon, disconnect first
        if (Photon.Pun.PhotonNetwork.IsConnected)
        {
            Photon.Pun.PhotonNetwork.Disconnect();
        }

        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>
    /// Call this method to set custom lore text before the scene starts.
    /// Useful if you want to pass lore from the game.
    /// </summary>
    public void SetLoreContent(string newLore)
    {
        loreContent = newLore;
    }

    /// <summary>
    /// Call this method to set custom credits before the scene starts.
    /// </summary>
    public void SetCreditsContent(string newCredits)
    {
        creditsContent = newCredits;
    }
}
