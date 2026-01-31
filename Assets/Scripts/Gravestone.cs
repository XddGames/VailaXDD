using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Collider))]
public class Gravestone : MonoBehaviourPunCallbacks
{
    [Header("Gravestone Settings")]
    [SerializeField] private string deceasedName;
    [SerializeField] private Renderer gravestoneRenderer;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material glowMaterial;
    
    private GraveyardMinigame minigameManager;
    private bool isGlowing = false;
    private bool isClickable = true;

    public string DeceasedName => deceasedName;

    private void Awake()
    {
        if (gravestoneRenderer == null)
        {
            gravestoneRenderer = GetComponent<Renderer>();
        }
    }

    public void Initialize(GraveyardMinigame manager, string name)
    {
        minigameManager = manager;
        deceasedName = name;
    }

    public void OnClicked(PlayerController player)
    {
        if (!isClickable || minigameManager == null)
            return;

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(RPC_ProcessClick), RpcTarget.AllBuffered, player.photonView.ViewID);
        }
        else
        {
            ProcessClick();
        }
    }

    [PunRPC]
    private void RPC_ProcessClick(int playerViewID)
    {
        ProcessClick();
    }

    private void ProcessClick()
    {
        minigameManager.OnGravestoneClicked(this);
    }

    public void SetGlowState(bool glow)
    {
        if (PhotonNetwork.IsConnected && photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_SetGlow), RpcTarget.AllBuffered, glow);
        }
        else if (!PhotonNetwork.IsConnected)
        {
            ApplyGlow(glow);
        }
    }

    [PunRPC]
    private void RPC_SetGlow(bool glow)
    {
        ApplyGlow(glow);
    }

    private void ApplyGlow(bool glow)
    {
        isGlowing = glow;
        
        if (gravestoneRenderer != null)
        {
            if (glow && glowMaterial != null)
            {
                gravestoneRenderer.material = glowMaterial;
            }
            else if (!glow && normalMaterial != null)
            {
                gravestoneRenderer.material = normalMaterial;
            }
            
            if (gravestoneRenderer.material.HasProperty("_EmissionColor"))
            {
                if (glow)
                {
                    gravestoneRenderer.material.EnableKeyword("_EMISSION");
                    gravestoneRenderer.material.SetColor("_EmissionColor", Color.yellow * 2f);
                }
                else
                {
                    gravestoneRenderer.material.DisableKeyword("_EMISSION");
                    gravestoneRenderer.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    public void SetClickable(bool clickable)
    {
        isClickable = clickable;
    }

    public void ResetGravestone()
    {
        if (PhotonNetwork.IsConnected && photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_Reset), RpcTarget.AllBuffered);
        }
        else if (!PhotonNetwork.IsConnected)
        {
            Reset();
        }
    }

    [PunRPC]
    private void RPC_Reset()
    {
        Reset();
    }

    private void Reset()
    {
        ApplyGlow(false);
        isClickable = true;
    }
}
