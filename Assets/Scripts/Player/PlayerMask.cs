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
            // Get PhotonView from parent or this GameObject
            photonView = GetComponentInParent<PhotonView>();
            if (photonView == null)
            {
                photonView = GetComponent<PhotonView>();
            }
            
            if (maskOverlayUI != null)
            {
                maskOverlayUI.SetActive(false);
            }
            
            if (physicalMask != null)
            {
                physicalMask.SetActive(false);
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
                photonView = GetComponentInParent<PhotonView>();
                if (photonView == null)
                {
                    photonView = GetComponent<PhotonView>();
                }
            }
            
            bool isLocalPlayer = photonView != null && photonView.IsMine;
            
            // Only show UI overlay for the local player
            if (maskOverlayUI != null)
            {
                maskOverlayUI.SetActive(isOn && isLocalPlayer);
            }
            
            // Handle physical 3D mask
            if (physicalMask != null)
            {
                // For local player, disable the entire physical mask GameObject
                // For remote players, enable it so they can see the mask
                if (isLocalPlayer)
                {
                    // Local player: keep mask off to not block their view
                    physicalMask.SetActive(false);
                }
                else
                {
                    // Remote players: show/hide based on mask state
                    physicalMask.SetActive(isOn);
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