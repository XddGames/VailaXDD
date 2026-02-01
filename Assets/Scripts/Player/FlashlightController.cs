using UnityEngine;
using Photon.Pun;

public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    public Light flashlightLight; 
    public AudioSource audioSource; 
    public AudioClip clickSound;

    private bool isEquipped = true; // Default to true so F works immediately
    private bool isOwnedByLocalPlayer = false; // Only true when held by local player - STARTS FALSE
    private PhotonView parentPhotonView; // Reference to player's PhotonView
    private bool ownerWasExplicitlySet = false; // Only true after SetOwner() is called
    private PlayerController playerController; // To check if player is alive

    void Awake()
    {
        // Ensure ownership is false by default until explicitly set via SetOwner()
        isOwnedByLocalPlayer = false;
        ownerWasExplicitlySet = false;
    }

    void Start()
    {
        if (flashlightLight != null)
            flashlightLight.enabled = false;
    }

    void OnEnable()
    {
        // ONLY re-check ownership if SetOwner was previously called
        // This prevents ground pickups from auto-detecting ownership
        if (ownerWasExplicitlySet && parentPhotonView != null)
        {
            isOwnedByLocalPlayer = parentPhotonView.IsMine;
            Debug.Log($"[{gameObject.name}] OnEnable: Re-checked ownership = {isOwnedByLocalPlayer}");
        }
        else
        {
            // Never picked up - stay unowned
            isOwnedByLocalPlayer = false;
        }
    }

    void Update()
    {
        // ONLY respond to input if this flashlight is owned by the local player
        if (!isOwnedByLocalPlayer) return;
        
        // Don't allow flashlight use when dead/spectating
        if (playerController != null && playerController.GetCurrentState() != PlayerState.Alive) return;

        // F toggles flashlight (only if equipped)
        if (isEquipped && Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log($"[{gameObject.name}] F pressed - isOwnedByLocalPlayer: {isOwnedByLocalPlayer}, parentPhotonView: {(parentPhotonView != null ? parentPhotonView.ViewID.ToString() : "null")}, IsMine: {(parentPhotonView != null ? parentPhotonView.IsMine.ToString() : "N/A")}");
            ToggleFlashlight();
        }
    }

    /// <summary>
    /// Called when this flashlight is picked up by a player
    /// </summary>
    public void SetOwner(PhotonView ownerPhotonView)
    {
        parentPhotonView = ownerPhotonView;
        ownerWasExplicitlySet = true; // Mark that ownership was explicitly set
        isOwnedByLocalPlayer = (ownerPhotonView != null && ownerPhotonView.IsMine);
        
        // Cache PlayerController reference for alive checks
        if (ownerPhotonView != null)
        {
            playerController = ownerPhotonView.GetComponent<PlayerController>();
        }
        Debug.Log($"FlashlightController: SetOwner called, isOwnedByLocalPlayer = {isOwnedByLocalPlayer}");
    }

    /// <summary>
    /// Called by InventoryManager to equip/unequip the flashlight
    /// </summary>
    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
        
        // Turn off light when unequipped
        if (!equipped && flashlightLight != null)
        {
            flashlightLight.enabled = false;
            
            // Sync with other players
            if (parentPhotonView != null && parentPhotonView.IsMine)
            {
                parentPhotonView.RPC(nameof(RPC_SyncFlashlight), RpcTarget.Others, false);
            }
        }

        // Sync equipped state with other players
        if (parentPhotonView != null && parentPhotonView.IsMine)
        {
            parentPhotonView.RPC(nameof(RPC_SyncEquipped), RpcTarget.Others, equipped);
        }
    }

    public bool IsEquipped() => isEquipped;
    
    public bool IsLightOn() => flashlightLight != null && flashlightLight.enabled;

    void ToggleFlashlight()
    {
        if (flashlightLight == null) return;
        
        bool newState = !flashlightLight.enabled;
        
        ApplyFlashlightState(newState);
        
        // Sync with other players via parent's PhotonView
        if (parentPhotonView != null && parentPhotonView.IsMine)
        {
            parentPhotonView.RPC("RPC_SyncFlashlightState", RpcTarget.Others, newState);
        }
    }

    void ApplyFlashlightState(bool state)
    {
        if (flashlightLight != null)
        {
            flashlightLight.enabled = state;
            Debug.Log($"Flashlight toggled: {state}");
        }

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }

    // Called by parent's RPC
    public void SyncFromNetwork(bool equipped, bool lightOn)
    {
        isEquipped = equipped;
        if (flashlightLight != null)
        {
            flashlightLight.enabled = lightOn;
        }
    }
}