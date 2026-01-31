using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(Collider))]
public class Gravestone : MonoBehaviourPunCallbacks
{
    [Header("Gravestone Settings")]
    [SerializeField] private string deceasedName;
    [SerializeField] private Renderer gravestoneRenderer;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private TextMeshPro nameText;
    
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
        
        UpdateNameDisplay();
    }
    
    private void UpdateNameDisplay()
    {
        if (nameText != null && !string.IsNullOrEmpty(deceasedName))
        {
            nameText.text = deceasedName;
        }
    }
    
    private void OnValidate()
    {
        UpdateNameDisplay();
    }

    public void Initialize(GraveyardMinigame manager, string name)
    {
        minigameManager = manager;
        deceasedName = name;
        UpdateNameDisplay();
    }

    public void OnClicked(PlayerController player)
    {
        Debug.Log($"[GRAVE] OnClicked chamado para {deceasedName}");
        Debug.Log($"[GRAVE] isClickable: {isClickable}, minigameManager: {minigameManager != null}");
        
        if (!isClickable || minigameManager == null)
        {
            Debug.Log($"[GRAVE] Clique ignorado! isClickable={isClickable}, manager={minigameManager != null}");
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            int playerViewID = -1;
            if (player != null && player.photonView != null)
            {
                playerViewID = player.photonView.ViewID;
            }

            if (photonView != null)
            {
                Debug.Log($"[GRAVE] Photon conectado, enviando RPC para {deceasedName} (playerViewID={playerViewID})");
                try
                {
                    photonView.RPC(nameof(RPC_ProcessClick), RpcTarget.AllBuffered, playerViewID);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GRAVE] RPC falhou, a processar localmente: {ex.Message}");
                    ProcessClick();
                }
            }
            else
            {
                Debug.LogWarning("[GRAVE] Gravestone sem PhotonView, processando clique localmente");
                ProcessClick();
            }
        }
        else
        {
            Debug.Log($"[GRAVE] Modo offline, processando clique diretamente para {deceasedName}");
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
        Debug.Log($"[GRAVE] ProcessClick chamado para {deceasedName}");
        Debug.Log($"[GRAVE] Manager é null? {minigameManager == null}");
        if (minigameManager != null)
        {
            Debug.Log($"[GRAVE] Chamando OnGravestoneClicked no manager");
            minigameManager.OnGravestoneClicked(this);
        }
    }

    public void SetGlowState(bool glow)
    {
        if (PhotonNetwork.IsConnected)
        {
            if (photonView != null)
            {
                try
                {
                    photonView.RPC(nameof(RPC_SetGlow), RpcTarget.AllBuffered, glow);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GRAVE] SetGlowState RPC falhou: {ex.Message}. Aplicando localmente.");
                    ApplyGlow(glow);
                }
            }
            else
            {
                Debug.LogWarning("[GRAVE] SetGlowState: photonView é null, aplicando glow localmente");
                ApplyGlow(glow);
            }
        }
        else
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
        if (PhotonNetwork.IsConnected)
        {
            if (photonView != null)
            {
                try
                {
                    photonView.RPC(nameof(RPC_Reset), RpcTarget.AllBuffered);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GRAVE] RPC_Reset falhou: {ex.Message}. Aplicando reset localmente.");
                    Reset();
                }
            }
            else
            {
                Debug.LogWarning("[GRAVE] ResetGravestone: photonView é null, aplicando reset localmente");
                Reset();
            }
        }
        else
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
