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
    [SerializeField] private TMP_Text nameText;
    
    private GraveyardMinigame minigameManager;
    private bool isGlowing = false;
    private bool isClickable = true;
    private float lastClickTime = 0f;
    private const float CLICK_COOLDOWN = 0.5f;

    public string DeceasedName => deceasedName;

    private void Awake()
    {
        if (gravestoneRenderer == null)
        {
            gravestoneRenderer = GetComponent<Renderer>();
        }
        
        UpdateNameDisplay();
    }
    
    private void Start()
    {
        // Ensure name is visible after all initialization
        UpdateNameDisplay();
    }
    
    private void UpdateNameDisplay()
    {
        if (nameText != null)
        {
            if (!string.IsNullOrEmpty(deceasedName))
            {
                nameText.text = deceasedName;
                nameText.gameObject.SetActive(true);
            }
            else
            {
                nameText.text = "???";
            }
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

    public void SetName(string name)
    {
        deceasedName = name;
        UpdateNameDisplay();
    }

    public void SetDisplayText(string displayText)
    {
        if (nameText != null)
        {
            nameText.text = displayText;
            nameText.gameObject.SetActive(true);
        }
    }

    public void OnClicked(PlayerController player)
    {
        
        if (!isClickable || minigameManager == null)
        {
            return;
        }

        // Prevent double-clicking
        if (Time.time - lastClickTime < CLICK_COOLDOWN)
        {
            return;
        }
        lastClickTime = Time.time;

        if (PhotonNetwork.IsConnected)
        {
            int playerViewID = -1;
            if (player != null && player.photonView != null)
            {
                playerViewID = player.photonView.ViewID;
            }

            if (photonView != null)
            {
                try
                {
                    photonView.RPC(nameof(RPC_ProcessClick), RpcTarget.AllBuffered, playerViewID);
                }
                catch (System.Exception ex)
                {
                    ProcessClick();
                }
            }
            else
            {
                ProcessClick();
            }
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
        if (minigameManager != null)
        {
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
                    ApplyGlow(glow);
                }
            }
            else
            {
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
                    Reset();
                }
            }
            else
            {
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
