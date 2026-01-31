using UnityEngine;
using Photon.Pun;

public class SpectatorCamera : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -5);
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private bool allowRotation = true;

    private Transform targetPlayer;
    private Camera spectatorCam;
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    private void Awake()
    {
        spectatorCam = GetComponent<Camera>();
        if (spectatorCam == null)
        {
            spectatorCam = GetComponentInChildren<Camera>();
        }
        
        // Disable by default - only enable when entering spectator mode
        enabled = false;
        if (spectatorCam != null)
        {
            spectatorCam.enabled = false;
        }
    }

    public void StartSpectating()
    {
        enabled = true; // Enable the component
        
        // Find an alive player to spectate
        FindAlivePlayer();
        
        if (spectatorCam != null)
        {
            spectatorCam.enabled = true;
        }
        
        Debug.Log($"Started spectating: {(targetPlayer != null ? targetPlayer.name : "No target found")}");
    }

    public void StopSpectating()
    {
        enabled = false; // Disable the component
        targetPlayer = null;
        
        if (spectatorCam != null)
        {
            spectatorCam.enabled = false;
        }
    }

    private void FindAlivePlayer()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        
        foreach (PlayerController pc in allPlayers)
        {
            // Don't spectate yourself
            PhotonView pv = pc.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine) continue;
            
            // Find alive player
            if (pc.GetCurrentState() == PlayerState.Alive)
            {
                targetPlayer = pc.transform;
                Debug.Log($"Spectating player: {pc.name}");
                return;
            }
        }
        
        Debug.LogWarning("No alive players found to spectate!");
    }

    private void LateUpdate()
    {
        if (targetPlayer == null)
        {
            // Try to find a player again
            FindAlivePlayer();
            return;
        }

        // Check if target is still alive
        PlayerController targetPC = targetPlayer.GetComponent<PlayerController>();
        if (targetPC != null && targetPC.GetCurrentState() != PlayerState.Alive)
        {
            // Target died, find another
            FindAlivePlayer();
            return;
        }

        // Follow the target
        if (allowRotation)
        {
            // Allow mouse rotation around target
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            currentPitch = Mathf.Clamp(currentPitch, -30f, 60f);

            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
            Vector3 rotatedOffset = rotation * offset;
            
            Vector3 desiredPosition = targetPlayer.position + rotatedOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            transform.LookAt(targetPlayer.position + Vector3.up * 1.5f);
        }
        else
        {
            // Simple follow behind target
            Vector3 desiredPosition = targetPlayer.position - targetPlayer.forward * offset.z + Vector3.up * offset.y;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            transform.LookAt(targetPlayer.position + Vector3.up * 1.5f);
        }
    }
}
