    using UnityEngine;
    using Photon.Pun;

    public class PlayerMask : MonoBehaviour
    {
        public bool HasMaskOn { get; private set; } = false;

        [Header("Visuals")]
        public GameObject maskOverlayUI;
        public GameObject physicalMask; // 3D mask model that other players see

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
            {
                maskOverlayUI.SetActive(false);
            }
            
            if (physicalMask != null)
            {
                physicalMask.SetActive(false);
                
                // Hide physical mask renderers for local player so it doesn't block their vision
                PhotonView photonView = GetComponent<PhotonView>();
                if (photonView != null && photonView.IsMine)
                {
                    Renderer[] renderers = physicalMask.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.enabled = false;
                    }
                    Debug.Log("Disabled physical mask renderers for local player");
                }
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        public float GetMaskEffect() => HasMaskOn ? detectionMultiplier : 1.0f;
        
        public void SetMaskState(bool isOn)
        {
            HasMaskOn = isOn;

            if (maskOverlayUI != null)
            {
                maskOverlayUI.SetActive(isOn);
                Debug.Log($"MASK HAS MASKED TO {isOn}");
            }
            
            // Show/hide physical 3D mask
            if (physicalMask != null)
            {
                physicalMask.SetActive(isOn);
            }

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