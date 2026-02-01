using UnityEngine;
using Photon.Pun;

public class FlashlightController : MonoBehaviourPun
{
    [Header("References")]
    public Light flashlightLight; 
    public AudioSource audioSource; 
    public AudioClip clickSound;

    void Start()
    {
        if (flashlightLight != null)
            flashlightLight.enabled = false;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }
    }

    void ToggleFlashlight()
    {
        bool newState = !flashlightLight.enabled;
        
        ApplyFlashlightState(newState);
        photonView.RPC(nameof(RPC_SyncFlashlight), RpcTarget.Others, newState);
    }

    [PunRPC]
    void RPC_SyncFlashlight(bool state)
    {
        ApplyFlashlightState(state);
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