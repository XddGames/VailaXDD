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

        private PhotonView photonView;
        private Renderer[] physicalMaskRenderers;

        void Start()
        {
            photonView = GetComponent<PhotonView>();
            
            if (maskOverlayUI != null)
            {
                maskOverlayUI.SetActive(false);
            }
            
            if (physicalMask != null)
            {
                physicalMask.SetActive(false);
                
                // Cache renderers for the physical mask
                physicalMaskRenderers = physicalMask.GetComponentsInChildren<Renderer>(true);
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        public float GetMaskEffect() => HasMaskOn ? detectionMultiplier : 1.0f;
        
        public void SetMaskState(bool isOn)
        {
            HasMaskOn = isOn;

            // Ensure photonView is initialized
            if (photonView == null)
            {
                photonView = GetComponent<PhotonView>();
            }
            
            bool isLocalPlayer = photonView != null && photonView.IsMine;
            
            Debug.Log($"SetMaskState called: isOn={isOn}, photonView={photonView != null}, IsMine={photonView?.IsMine}, isLocalPlayer={isLocalPlayer}, maskOverlayUI={maskOverlayUI != null}");
            
            // Only show UI overlay for the local player
            if (maskOverlayUI != null)
            {
                maskOverlayUI.SetActive(isOn && isLocalPlayer);
                Debug.Log($"MASK UI SET TO: {isOn && isLocalPlayer} (isOn={isOn}, isLocalPlayer={isLocalPlayer})");
            }
            else
            {
                Debug.LogError("maskOverlayUI is NULL!");
            }
            
            // Handle physical 3D mask - always activate GameObject, but control renderer visibility
            if (physicalMask != null)
            {
                physicalMask.SetActive(isOn);
                
                // For local player, hide the physical mask renderers so it doesn't block their view
                // For remote players, show the physical mask so they can see it
                if (physicalMaskRenderers != null)
                {
                    foreach (Renderer renderer in physicalMaskRenderers)
                    {
                        renderer.enabled = isOn && !isLocalPlayer;
                    }
                }
            }

            if (isOn)
            {
                if (equipSound != null) AudioSource.PlayClipAtPoint(equipSound, transform.position);

                // Only play breathing sound for local player
                if (isLocalPlayer && audioSource != null && breathingSound != null)
                {
                    audioSource.clip = breathingSound;
                    audioSource.loop = true;
                    audioSource.Play();
                }
            }
            else
            {
                if (isLocalPlayer && audioSource != null) audioSource.Stop();
            }
        }
    }