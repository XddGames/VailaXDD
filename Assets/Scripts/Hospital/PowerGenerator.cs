using UnityEngine;
using Photon.Pun;
using UnityEngine.Events;

public class PowerGenerator : MonoBehaviourPun
{
    public bool IsOn { get; private set; } = false;
    
    [Header("Settings")]
    public float timeToTurnOn = 3f;
    public UnityEvent<bool> OnStateChanged;

    [PunRPC]
    public void RPC_SetState(bool state)
    {
        IsOn = state;
        Debug.Log($"Generator {(IsOn ? "Started" : "Stopped")}!");
        
        OnStateChanged?.Invoke(IsOn);
    }

    public void EnemySabotage()
    {
        if (IsOn)
            photonView.RPC(nameof(RPC_SetState), RpcTarget.AllBuffered, false);
    }
}