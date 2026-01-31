using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMask))]
public class PlayerController : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private Camera playerCamera;
    private CharacterController characterController;
    private PlayerMask playerMask;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float groundFriction = 15f;
    [SerializeField] private float jumpForce = 1f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float airControlPercent = 1f;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float maxLookAngle = 80f;

    private const float GROUND_STICK_FORCE = -2f;
    private const float INPUT_THRESHOLD = 0.1f;
    private const float JUMP_GRAVITY_MULTIPLIER = 1.5f;
    private const float INTERACTION_COOLDOWN = 0.5f;

    private Vector3 velocity;
    private Vector3 horizontalVelocity;
    private float verticalRotation = 0f;
    private bool isGrounded;
    private bool lastInteractState;
    private float lastInteractionTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerMask = GetComponent<PlayerMask>();
    }

    private void Start()
    {
        //if (!photonView.IsMine && PhotonNetwork.IsConnected)
       /* {
            if (playerCamera != null)
                playerCamera.enabled = false;
            
            if (inputHandler != null)
                inputHandler.enabled = false;
            
            enabled = false;
            return;
        }*/

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("Main Camera not found! PlayerController disabled.");
                enabled = false;
                return;
            }
        }
    }

    private void Update()
    {
        //if (!photonView.IsMine && PhotonNetwork.IsConnected) return;
        if (inputHandler == null) return;

        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        HandleInteraction();
        HandleCameraRotation();
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
            // Debug - remove later
            Debug.Log($"Input: {input} | InputMag: {input.sqrMagnitude:F4} | Threshold: {INPUT_THRESHOLD * INPUT_THRESHOLD:F4} | Applying Friction: {input.sqrMagnitude < INPUT_THRESHOLD * INPUT_THRESHOLD}");
            
            if (input.sqrMagnitude < INPUT_THRESHOLD * INPUT_THRESHOLD)
            {
                // Apply friction by reducing velocity exponentially
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
        if (inputHandler.sprintInput && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD)
        {
            return sprintSpeed;
        }
        return walkSpeed;
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
}
