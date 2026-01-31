using UnityEngine;
using Photon.Pun;
public enum PlayerState
{
    Alive,
    WaitingRevive,
    Spectating,
    SinglePlayerDead
}
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMask))]

public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("References")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private Camera playerCamera;
    private CharacterController characterController;
    private PlayerMask playerMask;
    [Header("Revive Settings")]
    [SerializeField] private float reviveRange = 3f;
    [SerializeField] private float reviveTime = 5f; // Time to revive in seconds
    [SerializeField] private LayerMask playerLayerMask; // Set to Player layer
    private float reviveProgress = 0f;
    private PlayerController playerBeingRevived = null;
    private bool isBeingRevived = false;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float groundFriction = 15f;
    [SerializeField] private float jumpForce = 1f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float airControlPercent = 1f;
    [SerializeField] private LayerMask groundMask;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 20f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float staminaRegenDelay = 1f;

    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float maxLookAngle = 80f;

    private const float GROUND_STICK_FORCE = -2f;
    private const float INPUT_THRESHOLD = 0.1f;
    private const float JUMP_GRAVITY_MULTIPLIER = 1.5f;
    private const float INTERACTION_COOLDOWN = 0.5f;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 velocity;
    private Vector3 horizontalVelocity;
    private float verticalRotation = 0f;
    private bool isGrounded;
    private bool lastInteractState;
    private float lastInteractionTime;

    private float currentStamina;
    private float lastSprintTime;
    private PlayerState currentState;

    public PlayerState GetCurrentState()
    {
        return currentState;
    }
    private void Awake()
    {
        currentState = PlayerState.Alive;
        characterController = GetComponent<CharacterController>();
        playerMask = GetComponent<PlayerMask>();
        currentStamina = maxStamina;
    }

    private void Start()
    {
        // Setup for both local and remote players
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            // LOCAL PLAYER - Setup controls
            Debug.Log("LOCAL PLAYER - Setting up controls");

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Find or use existing InputHandler
            if (inputHandler == null)
            {
                inputHandler = FindAnyObjectByType<InputHandler>();
                if (inputHandler == null)
                {
                    Debug.LogError("InputHandler not found! PlayerController disabled.");
                    enabled = false;
                    return;
                }
            }

            inputHandler.enabled = true;

            // Setup camera for local player
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    playerCamera = Camera.main;
                }
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = true;

                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = true;
                }
            }

            Debug.Log($"Player setup complete. Camera: {playerCamera?.name}, InputHandler: {inputHandler?.name}");
        }
        else if (PhotonNetwork.IsConnected)
        {
            // REMOTE PLAYER - Disable camera and input only
            Debug.Log("REMOTE PLAYER - Disabling camera and input");

            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.enabled = false;
            }

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }

            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }

            // Keep the controller enabled but it won't process input
            // This allows gravity and physics to still work for visual sync
        }
    }
    private void HandleRemotePlayerPhysics()
    {
        if (characterController == null || !characterController.enabled) return;

        // Smoothly move towards network position
        Vector3 targetPosition = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
        Vector3 movement = targetPosition - transform.position;

        characterController.Move(movement);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
    }
    private void Update()
    {
        // Handle remote players differently
        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            // Remote players still need gravity applied for visual sync
            HandleRemotePlayerPhysics();
            return;
        }

        if (inputHandler == null) return;

        switch (currentState)
        {
            case PlayerState.Alive:
                HandleGroundCheck();
                HandleMovement();
                HandleJump();
                HandleInteraction();
                HandleCameraRotation();
                HandleStamina();
                HandleRevive();
                break;
            case PlayerState.WaitingRevive:
                HandleCameraRotation();
                break;
            case PlayerState.Spectating:
                break;
            case PlayerState.SinglePlayerDead:
                break;
        }
    }

    private void HandleRevive()
    {
        if (!inputHandler.interactInput)
        {
            if (playerBeingRevived != null)
            {
                photonView.RPC(nameof(RPC_CancelRevive), RpcTarget.All, playerBeingRevived.photonView.ViewID);
                playerBeingRevived = null;
                reviveProgress = 0f;
                Debug.Log("Stopped reviving - interact key released");
            }
            return;
        }

        if (playerBeingRevived == null)
        {
            PlayerController downedPlayer = FindDownedPlayerInRange();
            if (downedPlayer != null)
            {
                playerBeingRevived = downedPlayer;
                reviveProgress = 0f;

                Debug.Log($"Started reviving {downedPlayer.name}");
                // Tell everyone we're starting to revive this player
                photonView.RPC(nameof(RPC_StartRevive), RpcTarget.All, downedPlayer.photonView.ViewID);
            }
            else
            {
                // Debug: No downed player found
                if (Time.frameCount % 60 == 0) // Log every second
                {
                    Debug.Log($"Looking for downed players in range {reviveRange}m...");
                }
            }
            return;
        }

        // Check if still in range
        float distance = Vector3.Distance(transform.position, playerBeingRevived.transform.position);
        if (distance > reviveRange)
        {
            // Too far away, cancel
            photonView.RPC(nameof(RPC_CancelRevive), RpcTarget.All, playerBeingRevived.photonView.ViewID);
            playerBeingRevived = null;
            reviveProgress = 0f;
            return;
        }

        // Continue reviving
        reviveProgress += Time.deltaTime;

        if (reviveProgress >= reviveTime)
        {
            // Revive complete!
            photonView.RPC(nameof(RPC_CompleteRevive), RpcTarget.All, playerBeingRevived.photonView.ViewID);
            playerBeingRevived = null;
            reviveProgress = 0f;
        }


    }
    private PlayerController FindDownedPlayerInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, reviveRange, playerLayerMask);
        
        if (Time.frameCount % 60 == 0 && colliders.Length > 0) // Debug every second
        {
            Debug.Log($"Found {colliders.Length} colliders in revive range");
        }

        foreach (Collider col in colliders)
        {
            if (col.transform == transform) continue; // Skip self

            PlayerController pc = col.GetComponent<PlayerController>();
            if (pc != null)
            {
                PlayerState pcState = pc.GetCurrentState();
                bool beingRevived = pc.isBeingRevived;
                
                if (Time.frameCount % 60 == 0) // Debug
                {
                    Debug.Log($"Found player {pc.name}: State={pcState}, BeingRevived={beingRevived}");
                }
                
                if (pcState == PlayerState.WaitingRevive && !beingRevived)
                {
                    Debug.Log($"Found downed player to revive: {pc.name}");
                    return pc;
                }
            }
        }

        return null;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Local player - send data to network
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(velocity);
            stream.SendNext((int)currentState);
        }
        else
        {
            // Remote player - receive data from network
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();

            int stateInt = (int)stream.ReceiveNext();
            currentState = (PlayerState)stateInt;

            // Smooth interpolation
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            networkPosition += networkVelocity * lag;
        }
    }

    [PunRPC]
    public void RPC_KillPlayer()
    {
        currentState = PlayerState.WaitingRevive;
        Debug.Log($"<color=red>Player {gameObject.name} was killed by enemy (RPC received on {(photonView.IsMine ? "LOCAL" : "REMOTE")} client)</color>");
    }

    [PunRPC]
    private void RPC_StartRevive(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            PlayerController target = targetView.GetComponent<PlayerController>();
            if (target != null)
            {
                target.isBeingRevived = true;
                Debug.Log($"Started reviving {target.name}");
            }
        }
    }

    [PunRPC]
    private void RPC_CancelRevive(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            PlayerController target = targetView.GetComponent<PlayerController>();
            if (target != null)
            {
                target.isBeingRevived = false;
                Debug.Log($"Cancelled reviving {target.name}");
            }
        }
    }

    [PunRPC]
    private void RPC_CompleteRevive(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            PlayerController target = targetView.GetComponent<PlayerController>();
            if (target != null)
            {
                target.ChangeState(PlayerState.Alive);
                target.isBeingRevived = false;
                Debug.Log($"Revived {target.name}!");
            }
        }
    }

    public float GetReviveProgress()
    {
        return reviveProgress / reviveTime;
    }

    public bool IsReviving()
    {
        return playerBeingRevived != null;
    }

    public bool IsBeingRevived()
    {
        return isBeingRevived;
    }
    public PlayerState ChangeState(PlayerState newState)
    {
        PlayerState tempState = currentState;
        currentState = newState;
        return tempState;

    }

    private void HandleGroundCheck()
    {
        Vector3 spherePosition = transform.position - Vector3.up * (characterController.height * 0.5f - characterController.radius);
        isGrounded = Physics.CheckSphere(spherePosition, characterController.radius + groundCheckDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = GROUND_STICK_FORCE;
        }
    }

    private void HandleMovement()
    {
        Vector2 input = inputHandler.movementInput;

        // Debug - remove later
        if (input.sqrMagnitude > 0.001f)
        {
            Debug.Log($"Input detected: {input} | Magnitude: {input.magnitude} | Grounded: {isGrounded}");
        }

        Transform camTransform = playerCamera.transform;
        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * input.y + right * input.x;

        float moveMagnitudeSqr = moveDirection.sqrMagnitude;
        if (moveMagnitudeSqr > 1f)
        {
            moveDirection /= Mathf.Sqrt(moveMagnitudeSqr);
        }

        float currentSpeed = CalculateSpeed();
        Vector3 targetVelocity = moveDirection * currentSpeed;

        if (isGrounded)
        {
            if (input.sqrMagnitude < INPUT_THRESHOLD * INPUT_THRESHOLD)
            {
                float frictionFactor = Mathf.Max(0f, 1f - groundFriction * Time.deltaTime);
                horizontalVelocity *= frictionFactor;

                Debug.Log($"Friction applied. HVel after: {horizontalVelocity.magnitude:F2}");

                // Stop completely when very slow
                if (horizontalVelocity.sqrMagnitude < 0.1f)
                {
                    horizontalVelocity = Vector3.zero;
                }
            }
            else
            {
                horizontalVelocity = targetVelocity;
                Debug.Log($"Setting velocity to target: {targetVelocity.magnitude:F2}");
            }
        }
        else
        {
            Vector3 airControl = targetVelocity * airControlPercent;
            horizontalVelocity += airControl * Time.deltaTime;

            float maxAirSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            if (horizontalVelocity.magnitude > maxAirSpeed)
            {
                horizontalVelocity = horizontalVelocity.normalized * maxAirSpeed;
            }
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMovement = horizontalVelocity * Time.deltaTime;
        finalMovement.y = velocity.y * Time.deltaTime;

        characterController.Move(finalMovement);
    }

    private float CalculateSpeed()
    {
        if (inputHandler.sprintInput && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD && currentStamina > 0f)
        {
            return sprintSpeed;
        }
        return walkSpeed;
    }

    private void HandleStamina()
    {
        bool isSprinting = inputHandler.sprintInput && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD && currentStamina > 0f;

        if (isSprinting && isGrounded)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
            lastSprintTime = Time.time;
        }
        else if (Time.time >= lastSprintTime + staminaRegenDelay)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);
        }
    }

    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    public float GetMaxStamina()
    {
        return maxStamina;
    }

    public float GetStaminaPercentage()
    {
        return currentStamina / maxStamina;
    }

    private void HandleJump()
    {
        if (inputHandler.jumpInput && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * JUMP_GRAVITY_MULTIPLIER * Mathf.Abs(gravity));
        }
    }

    private void HandleInteraction()
    {
        if (inputHandler.interactInput && !lastInteractState)
        {
            if (Time.time >= lastInteractionTime + INTERACTION_COOLDOWN)
            {
                ToggleMask();
                lastInteractionTime = Time.time;
            }
        }
        lastInteractState = inputHandler.interactInput;
    }

    private void ToggleMask()
    {
        if (!photonView.IsMine) return;
        if (playerMask == null) return;

        bool newState = !playerMask.HasMaskOn;
        playerMask.SetMaskState(newState);

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC(nameof(SyncMaskState), RpcTarget.OthersBuffered, newState);
        }
    }

    [PunRPC]
    private void SyncMaskState(bool state)
    {
        if (playerMask != null)
        {
            playerMask.SetMaskState(state);
        }
    }

    private void HandleCameraRotation()
    {
        Vector2 lookInput = inputHandler.lookInput;

        transform.Rotate(0f, lookInput.x * mouseSensitivity, 0f, Space.Self);

        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);

        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    public bool IsMoving()
    {
        return inputHandler != null && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD;
    }

    public bool IsSprinting()
    {
        return inputHandler != null && inputHandler.sprintInput && IsMoving();
    }


    public float GetCurrentSpeed()
    {
        return CalculateSpeed();
    }

    private void OnGUI()
    {
        if (playerBeingRevived != null)
        {
            float progress = reviveProgress / reviveTime;
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height - 100, 200, 30), "");
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height - 100, 200 * progress, 30), $"Reviving... {progress * 100:F0}%");
        }

        if (isBeingRevived)
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 30), "Being revived...");
        }
    }
}
