using UnityEngine;
using Photon.Pun;

public class FlashlightController : MonoBehaviourPun
{
    [Header("References")]
    public Light flashlightLight; 
    public AudioSource audioSource; 
    public AudioClip clickSound;

    private bool isEquipped = true; // Default to true so F works immediately

    void Start()
    {
        if (flashlightLight != null)
            flashlightLight.enabled = false;
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;

        // F toggles flashlight (only if equipped)
        if (isEquipped && Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }
    }

    /// <summary>
    /// Called by InventoryManager to equip/unequip the flashlight
    /// </summary>
    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
        
        // Show/hide the entire flashlight GameObject
        gameObject.SetActive(equipped);
        
        // Turn off light when unequipped
        if (!equipped && flashlightLight != null)
        {
            flashlightLight.enabled = false;
            
            // Sync with other players
            if (photonView != null && photonView.IsMine)
            {
                photonView.RPC(nameof(RPC_SyncFlashlight), RpcTarget.Others, false);
            }
        }

        // Sync equipped state with other players
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_SyncEquipped), RpcTarget.Others, equipped);
        }
    }

    public bool IsEquipped() => isEquipped;

    void ToggleFlashlight()
    {
        bool newState = !flashlightLight.enabled;
        
        ApplyFlashlightState(newState);
        if (photonView != null)
        {
            photonView.RPC(nameof(RPC_SyncFlashlight), RpcTarget.Others, newState);
        }
    }

    [PunRPC]
    void RPC_SyncFlashlight(bool state)
    {
        ApplyFlashlightState(state);
    }

    [PunRPC]
    void RPC_SyncEquipped(bool equipped)
    {
        isEquipped = equipped;
        
        // Show/hide the entire flashlight GameObject
        gameObject.SetActive(equipped);
        
        if (!equipped && flashlightLight != null)
            flashlightLight.enabled = false;
    }

    void ApplyFlashlightState(bool state)
    {
        if (flashlightLight != null)
        {
            flashlightLight.enabled = state;
        }

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
}