using UnityEngine;

public class PlayerMask : MonoBehaviour
{
    public bool HasMaskOn { get; private set; } = false;

    [Header("Visuals")]
    public GameObject maskOverlayUI;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip breathingSound;
    public AudioClip equipSound;

    [Header("Mechanics")]
    [Range(0f, 1f)]
    public float detectionMultiplier = 0.2f;

    void Start()
    {
        if (maskOverlayUI != null) 
            maskOverlayUI.SetActive(false);
            
        if (audioSource == null) 
            audioSource = GetComponent<AudioSource>();
    }

    public float GetMaskEffect() => HasMaskOn ? detectionMultiplier : 1.0f;
    public void SetMaskState(bool isOn)
    {
        HasMaskOn = isOn;
        Debug.Log($"SETMASKSTATE: isOn:{isOn} maskOverlayUI:{maskOverlayUI!=null}");
        if (maskOverlayUI != null)
            maskOverlayUI.SetActive(isOn);

        if (isOn)
        {
            if (equipSound != null) AudioSource.PlayClipAtPoint(equipSound, transform.position);
            
            if (audioSource != null && breathingSound != null)
            {
                audioSource.clip = breathingSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource != null) audioSource.Stop();
        }
    }

}