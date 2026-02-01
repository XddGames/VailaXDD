using UnityEngine;
using Photon.Pun;
using System.Diagnostics;
using System.Collections.Generic;
public enum PlayerState
{
    Alive,
    WaitingRevive,
    Spectating,
    SinglePlayerDead
}
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("References")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private Camera playerCamera;
    [SerializeField] public PlayerMask playerMask;
    [SerializeField] private GameObject playerUI; // Drag your player UI Canvas here
    [SerializeField] private Animator playerAnimator;
    private CharacterController characterController;
    
    // Animation parameter hashes for performance
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int AnimIsDead = Animator.StringToHash("IsDead");
    private static readonly int AnimIsReviving = Animator.StringToHash("IsReviving");
    private static readonly int AnimEmote = Animator.StringToHash("Emote");
    private static readonly int AnimIsSprinting = Animator.StringToHash("IsSprinting");
    
    // Animation state name hashes for CrossFade
    private static readonly int StateIdle = Animator.StringToHash("PlayerIddle");
    private static readonly int StateWalk = Animator.StringToHash("PlayerWalk");
    private static readonly int StateRun = Animator.StringToHash("PlayerRun");
    private static readonly int StateJumpUp = Animator.StringToHash("JumpingUp");
    private static readonly int StateJumpRun = Animator.StringToHash("Jumping_Running");
    private static readonly int StateDying = Animator.StringToHash("PlayerDying");
    private static readonly int StateEmote1 = Animator.StringToHash("PlayerEmote1");
    [Header("Revive Settings")]
    [SerializeField] private float reviveRange = 3f;
    [SerializeField] private float reviveTime = 5f; // Time to revive in seconds
    [SerializeField] private float reviveTimeLimit = 120f; // Time before permanent death
    [SerializeField] private LayerMask playerLayerMask; // Set to Player layer
    private float reviveProgress = 0f;
    private float reviveTimer = 0f; // Countdown timer
    private PlayerController playerBeingRevived = null;
    private bool isBeingRevived = false;
    
    [Header("Spectator")]
    private SpectatorCamera spectatorCamera;

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
    [SerializeField] private float jumpStaminaCost = 15f;

    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("Death Camera Settings")]
    [SerializeField] private Transform headBone; // Assign the head bone from the rig
    [SerializeField] private Vector3 deathCameraOffset = new Vector3(0f, 0.1f, 0.1f); // Offset from head bone
    [SerializeField] private float deathCameraFollowSpeed = 10f;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraFollowingHead = false;

    [Header("Generator Settings")]
    [SerializeField] private float interactRange = 10f;
    [SerializeField] private LayerMask generatorLayerMask;
    private PowerGenerator currentGenerator = null;
    private float generatorProgress = 0f;

    [Header("Graveyard Minigame")]
    [SerializeField] private LayerMask gravestoneLayerMask;
    private GraveyardMinigame currentGraveyardMinigame;

    [Header("Paper Settings")]
    public DiaryUI diaryUI; // Drag the 'Items' object here
    public List<int> collectedPageIDs = new List<int>(); // Stores 1, 2, 3
    private bool isDiaryOpen = false;
    [SerializeField] private LayerMask paperLayerMask; // Set this to a new Layer "Paper"

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
    private bool lastInteractStateForGrave;
    private float lastInteractionTime;

    private float currentStamina;
    private float lastSprintTime;
    private PlayerState currentState;
    private bool infiniteStamina = false;
    private bool lastJumpInput = false;
    private bool speedToggleActive = false;
    private float savedWalkSpeed;
    private float savedSprintSpeed;
    [SerializeField] private float toggleSpeed = 25f; // speed used when toggled on
    
    // Animation state tracking
    private bool jumpedWhileSprinting = false; // Track if jump started while sprinting
    private bool isJumping = false; // True while actively in a jump (from press until landing)
    private int currentEmote = 0;
    private bool deathAnimationPlayed = false; // Track if dying animation finished
    private bool isPlayingEmote = false;
    private int lastAnimState = 0; // Track current animation to avoid redundant CrossFade calls
    private float deathPositionY = 0f; // Store Y position when player dies
    private List<int> papersPickedUp;


    public PlayerState GetCurrentState()
    {
        return currentState;
    }
    private void Awake()
    {
        currentState = PlayerState.Alive;
        characterController = GetComponent<CharacterController>();
        currentStamina = maxStamina;
        spectatorCamera = GetComponent<SpectatorCamera>();
        
        // Get animator if not assigned
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        papersPickedUp = new List<int>();
        // Setup for both local and remote players
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            // LOCAL PLAYER - Setup controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Find or use existing InputHandler
            if (inputHandler == null)
            {
                inputHandler = FindAnyObjectByType<InputHandler>();
                if (inputHandler == null)
                {
                    // Try to get InputHandler from this GameObject
                    inputHandler = GetComponent<InputHandler>();
                    if (inputHandler == null)
                    {
                        // Create InputHandler on this GameObject
                        inputHandler = gameObject.AddComponent<InputHandler>();
                    }
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
                
                // Store original camera transform for death camera system
                originalCameraParent = playerCamera.transform.parent;
                originalCameraLocalPos = playerCamera.transform.localPosition;
                originalCameraLocalRot = playerCamera.transform.localRotation;
                
                // Try to find head bone automatically if not assigned
                if (headBone == null && playerAnimator != null)
                {
                    headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
                }

                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = true;
                }
            }
        }
        else if (PhotonNetwork.IsConnected)
        {
            // REMOTE PLAYER - Disable camera and input only
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
        }
    }

    private void HandlePaperInteraction()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 4f, paperLayerMask);
            
        foreach (Collider hit in hits)
        {
            PagePickup paper = hit.GetComponent<PagePickup>();
            if (paper != null)
            {
                UnityEngine.Debug.Log($"Picked up Paper ID: {paper.pieceID}");
                papersPickedUp.Add(paper.pieceID); 
                UnityEngine.Debug.Log(papersPickedUp);
                paper.OnPickedUp(this);
                return; 
            }
        }
        
        // Note: lastInteractState is updated in HandleInteraction(), 
        // so ensure HandleInteraction() is called AFTER this method in Update(),
        // OR manage the state update carefully if they share the same key.
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
    
    private void LateUpdate()
    {
        if (!photonView.IsMine && PhotonNetwork.IsConnected) return;
        if (playerCamera == null) return;
        
        bool isDead = currentState == PlayerState.WaitingRevive || currentState == PlayerState.SinglePlayerDead;
        
        if (isDead)
        {
            HandleDeathCamera();
        }
        else if (isCameraFollowingHead)
        {
            // Restore camera to original position when revived
            RestoreCameraPosition();
        }
    }
    
    private void HandleDeathCamera()
    {
        if (headBone == null) return;
        
        isCameraFollowingHead = true;
        
        // Unparent camera so it can follow the head freely
        if (playerCamera.transform.parent == originalCameraParent)
        {
            playerCamera.transform.SetParent(null, true);
        }
        
        // Calculate target position: slightly above the head
        Vector3 targetPosition = headBone.position + Vector3.up * deathCameraOffset.y + headBone.forward * deathCameraOffset.z;
        
        // When dead and lying on the ground, camera should look UP at the sky
        // Use a rotation that looks straight up with a slight forward tilt
        Quaternion targetRotation;
        
        if (deathAnimationPlayed)
        {
            // Player is lying on ground - look straight up at the sky
            targetRotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
        }
        else
        {
            // During dying animation - follow head more closely but still trending upward
            Vector3 lookDirection = Vector3.Lerp(headBone.up, Vector3.up, 0.5f);
            targetRotation = Quaternion.LookRotation(lookDirection, -headBone.forward);
        }
        
        // Smoothly interpolate camera position and rotation
        float currentSpeed = deathAnimationPlayed ? deathCameraFollowSpeed * 0.5f : deathCameraFollowSpeed;
        
        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, 
            targetPosition, 
            Time.deltaTime * currentSpeed
        );
        
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, 
            targetRotation, 
            Time.deltaTime * currentSpeed
        );
    }
    
    private void RestoreCameraPosition()
    {
        // Reparent camera back to original parent
        if (playerCamera.transform.parent != originalCameraParent)
        {
            playerCamera.transform.SetParent(originalCameraParent, true);
        }
        
        // Smoothly restore to original local position and rotation
        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            originalCameraLocalPos,
            Time.deltaTime * deathCameraFollowSpeed
        );
        
        playerCamera.transform.localRotation = Quaternion.Slerp(
            playerCamera.transform.localRotation,
            originalCameraLocalRot,
            Time.deltaTime * deathCameraFollowSpeed
        );
        
        // Check if close enough to original position
        if (Vector3.Distance(playerCamera.transform.localPosition, originalCameraLocalPos) < 0.01f)
        {
            playerCamera.transform.localPosition = originalCameraLocalPos;
            playerCamera.transform.localRotation = originalCameraLocalRot;
            isCameraFollowingHead = false;
            verticalRotation = 0f; // Reset vertical rotation
        }
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

        if (Input.GetKeyDown(KeyCode.L)) // cheat
        {
            infiniteStamina = !infiniteStamina;
            if (infiniteStamina)
            {
                currentStamina = maxStamina;
            }

            collectedPageIDs.Add(1);
            collectedPageIDs.Add(3);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleSpeed();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            ToggleDiaryState();
        }
        
        if (Input.GetKeyDown(KeyCode.G)) // mask
        {
            ToggleMask();
        }

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
                HandleGenerator();
                HandlePaperInteraction();
                HandleGraveyardInteraction();
                HandleEmoteInput();
                break;
            case PlayerState.WaitingRevive:
                // Don't handle normal camera rotation - death camera handles it in LateUpdate
                HandleWaitingRevive();
                HandleDeathPhysics(); // Apply gravity while dead
                break;
            case PlayerState.Spectating:
                // Spectator mode - camera is handled by SpectatorCamera component
                break;
            case PlayerState.SinglePlayerDead:
                break;
        }
        
        // Always update animations
        UpdateAnimations();
    }

    private void HandleEmoteInput()
    {
        // Press F1 for emote (twerk dance) - only when grounded, not moving, and not already emoting
        if (Input.GetKeyDown(KeyCode.F1) && isGrounded && !IsMoving() && !isPlayingEmote)
        {
            TriggerEmote(1);
        }
        
        // Cancel emote if player starts moving
        if (isPlayingEmote && IsMoving())
        {
            CancelEmote();
        }
    }

    private void TriggerEmote(int emoteId)
    {
        if (playerAnimator == null) return;
        
        isPlayingEmote = true;
        currentEmote = emoteId;
        
        // Force play emote animation
        playerAnimator.CrossFade(StateEmote1, 0.1f);
        lastAnimState = StateEmote1;
    }

    private void CancelEmote()
    {
        isPlayingEmote = false;
        currentEmote = 0;
        lastAnimState = 0; // Reset to force animation update
    }
    
    private void CheckEmoteFinished()
    {
        if (!isPlayingEmote || playerAnimator == null) return;
        
        AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);
        
        // Check if emote animation finished (normalizedTime >= 1 means one full loop)
        if (stateInfo.shortNameHash == StateEmote1 && stateInfo.normalizedTime >= 0.95f)
        {
            CancelEmote();
        }
    }

    private void UpdateAnimations()
    {
        if (playerAnimator == null) return;
        
        // Check if emote finished
        CheckEmoteFinished();

        // Determine current movement state
        bool isMoving = inputHandler != null && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD;
        bool isSprinting = inputHandler != null && inputHandler.sprintInput && isMoving && currentStamina > 0f;
        bool isDead = currentState == PlayerState.WaitingRevive || currentState == PlayerState.SinglePlayerDead;
        
        // Determine target animation state (priority order: Death > Jump > Emote > Movement)
        int targetState = 0;
        
        // 1. DEATH - Highest priority
        if (isDead)
        {
            // Animation was started in RPC_KillPlayer
            // Just check if it finished and freeze
            if (!deathAnimationPlayed)
            {
                AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);
                
                // Check if dying animation finished (normalizedTime >= 1 means finished)
                if (stateInfo.normalizedTime >= 0.95f)
                {
                    // Animation finished - freeze on last frame
                    deathAnimationPlayed = true;
                    playerAnimator.speed = 0f; // Freeze the animator
                }
            }
            // Don't set targetState - animation is already playing or frozen
        }
        // 2. JUMPING - Use isJumping flag set in HandleJump()
        else if (isJumping)
        {
            // Use the sprint state from when jump started
            targetState = jumpedWhileSprinting ? StateJumpRun : StateJumpUp;
        }
        // 3. EMOTE - Only when not moving and grounded
        else if (isPlayingEmote && isGrounded && !isMoving)
        {
            targetState = StateEmote1;
        }
        // 4. MOVEMENT - Normal ground movement
        else
        {
            // Cancel emote if we get here (player moved or jumped)
            if (isPlayingEmote)
            {
                CancelEmote();
            }
            
            if (isSprinting)
            {
                targetState = StateRun;
            }
            else if (isMoving)
            {
                targetState = StateWalk;
            }
            else
            {
                targetState = StateIdle;
            }
        }
        
        // Only change animation if target state is different
        if (targetState != 0 && targetState != lastAnimState)
        {
            float transitionTime = 0.15f;
            
            // Faster transitions for some states
            if (targetState == StateJumpUp || targetState == StateJumpRun)
            {
                transitionTime = 0.05f; // Quick transition to jump
            }
            else if (targetState == StateDying)
            {
                transitionTime = 0.1f;
            }
            
            playerAnimator.CrossFade(targetState, transitionTime);
            lastAnimState = targetState;
        }
        
        // Reset death tracking when revived
        if (!isDead && deathAnimationPlayed)
        {
            deathAnimationPlayed = false;
            lastAnimState = 0; // Force animation update
            playerAnimator.speed = 1f; // Restore animator speed
        }
        
        // Also update the parameters for network sync (other players use parameters)
        float speedParam = isSprinting ? 1f : (isMoving ? 0.5f : 0f);
        playerAnimator.SetFloat(AnimSpeed, speedParam);
        playerAnimator.SetBool(AnimIsGrounded, isGrounded);
        playerAnimator.SetBool(AnimIsJumping, isJumping);
        playerAnimator.SetBool(AnimIsSprinting, jumpedWhileSprinting || isSprinting);
        playerAnimator.SetBool(AnimIsDead, isDead);
        playerAnimator.SetBool(AnimIsReviving, deathAnimationPlayed);
        playerAnimator.SetInteger(AnimEmote, currentEmote);
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
                photonView.RPC(nameof(RPC_StartRevive), RpcTarget.All, downedPlayer.photonView.ViewID);
            }
            return;
        }

        // Check if still in range
        float distance = Vector3.Distance(transform.position, playerBeingRevived.transform.position);
        if (distance > reviveRange)
        {
            photonView.RPC(nameof(RPC_CancelRevive), RpcTarget.All, playerBeingRevived.photonView.ViewID);
            playerBeingRevived = null;
            reviveProgress = 0f;
            return;
        }

        // Continue reviving
        reviveProgress += Time.deltaTime;

        if (reviveProgress >= reviveTime)
        {
            photonView.RPC(nameof(RPC_CompleteRevive), RpcTarget.All, playerBeingRevived.photonView.ViewID);
            playerBeingRevived = null;
            reviveProgress = 0f;
        }
    }

    private void HandleWaitingRevive()
    {
        reviveTimer += Time.deltaTime;
        
        if (reviveTimer >= reviveTimeLimit)
        {
            photonView.RPC(nameof(RPC_EnterSpectator), RpcTarget.All);
        }
    }

    private void HandleGenerator()
    {
        //UnityEngine.Debug.Log($"currentGenerator={currentGenerator != null}");
        if (playerBeingRevived != null) return;

        if (!inputHandler.interactInput)
        {
            if (currentGenerator != null)
            {
                currentGenerator = null;
                generatorProgress = 0f;
            }
            return;
        }

        if (currentGenerator == null)
        {
            PowerGenerator foundGen = FindGeneratorInRange();
            
            if (foundGen != null && !foundGen.IsOn)
            {
                currentGenerator = foundGen;
                generatorProgress = 0f;
            }
            return;
        }

        float distance = Vector3.Distance(transform.position, currentGenerator.transform.position);
        if (distance > interactRange)
        {
            currentGenerator = null;
            generatorProgress = 0f;
            return;
        }

        generatorProgress += Time.deltaTime;
        if (generatorProgress >= currentGenerator.timeToTurnOn)
        {
            currentGenerator.photonView.RPC(nameof(PowerGenerator.RPC_SetState), RpcTarget.AllBuffered, true);
            
            currentGenerator = null;
            generatorProgress = 0f;
        }
    }

    private PowerGenerator FindGeneratorInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactRange, generatorLayerMask);
        
        foreach (Collider col in colliders)
        {
            PowerGenerator gen = col.GetComponent<PowerGenerator>();
            if (gen != null && !gen.IsOn)
                return gen;
        }
        return null;
    }

    private PlayerController FindDownedPlayerInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, reviveRange, playerLayerMask);

        foreach (Collider col in colliders)
        {
            if (col.transform == transform) continue;

            PlayerController pc = col.GetComponent<PlayerController>();
            if (pc != null)
            {
                PlayerState pcState = pc.GetCurrentState();
                bool beingRevived = pc.isBeingRevived;
                
                if (pcState == PlayerState.WaitingRevive && !beingRevived)
                {
                    return pc;
                }
            }
        }

        return null;
    }

    private void HandleGraveyardInteraction()
    {
        // Use GetKeyDown for instant response
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (currentGraveyardMinigame == null)
        {
            currentGraveyardMinigame = FindAnyObjectByType<GraveyardMinigame>();
        }

        if (currentGraveyardMinigame != null && currentGraveyardMinigame.IsMinigameActive())
        {
            Gravestone nearbyGravestone = currentGraveyardMinigame.FindGravestoneInRange(transform.position);
            
            if (nearbyGravestone != null)
            {
                nearbyGravestone.OnClicked(this);
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(velocity);
            stream.SendNext((int)currentState);
            
            // Sync animation state and normalized time for perfect sync
            if (playerAnimator != null)
            {
                AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);
                stream.SendNext(stateInfo.shortNameHash); // Current playing animation
                stream.SendNext(stateInfo.normalizedTime % 1f); // Current time in animation (0-1)
                stream.SendNext(playerAnimator.speed); // Animator speed (for death freeze)
            }
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();

            int stateInt = (int)stream.ReceiveNext();
            PlayerState networkState = (PlayerState)stateInt;
            if (currentState == PlayerState.Alive && networkState != PlayerState.Alive)
            {
                currentState = networkState;
            }

            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            networkPosition += networkVelocity * lag;
            
            // Receive and apply animation - just mirror exactly what they're playing
            if (playerAnimator != null)
            {
                int netAnimState = (int)stream.ReceiveNext();
                float netNormalizedTime = (float)stream.ReceiveNext();
                float netAnimSpeed = (float)stream.ReceiveNext();
                
                playerAnimator.speed = netAnimSpeed;
                
                // Just play exactly what they're playing
                if (netAnimState != 0)
                {
                    playerAnimator.Play(netAnimState, 0, netNormalizedTime);
                }
            }
        }
    }

    [PunRPC]
    public void RPC_KillPlayer()
    {
        PlayerState oldState = currentState;
        currentState = PlayerState.WaitingRevive;
        reviveTimer = 0f;
        
        // Store the Y position when dying to prevent floating
        deathPositionY = transform.position.y;
        
        // Clear inventory and destroy held items
        InventoryManager inventory = GetComponent<InventoryManager>();
        if (inventory != null)
        {
            inventory.OnPlayerDeath();
        }
        
        // Disable CharacterController to prevent it from interfering with death animation
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // Disable Root Motion to prevent animation from moving the player
        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.speed = 1f; // Ensure speed is normal
            
            // Force play the dying animation immediately
            playerAnimator.Play(StateDying, 0, 0f);
            lastAnimState = StateDying;
        }
        
        deathAnimationPlayed = false; // Reset this so we can detect when animation finishes
    }
    
    private void HandleDeathPhysics()
    {
        // Prevent the player from going UP - clamp to death position or lower
        if (transform.position.y > deathPositionY)
        {
            transform.position = new Vector3(transform.position.x, deathPositionY, transform.position.z);
        }
        
        // Apply gravity manually when CharacterController is disabled
        // Check if we're above the ground
        float rayDistance = 10f;
        RaycastHit hit;
        
        // Cast a ray downward from the player's position
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, rayDistance, groundMask))
        {
            // Calculate how high we are above the ground
            float groundY = hit.point.y;
            float playerY = transform.position.y;
            float heightAboveGround = playerY - groundY;
            
            // If we're above the ground, move down
            if (heightAboveGround > 0.05f)
            {
                // Apply gravity - move down smoothly
                float fallSpeed = Mathf.Abs(gravity) * Time.deltaTime;
                float newY = Mathf.Max(groundY, playerY - fallSpeed);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }
        else
        {
            // No ground detected, just apply gravity
            transform.position += Vector3.up * gravity * Time.deltaTime;
        }
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
                target.reviveTimer = 0f;
                target.deathAnimationPlayed = false; // Reset death animation flag
                
                // Re-enable CharacterController
                if (target.characterController != null)
                {
                    target.characterController.enabled = true;
                }
                
                // Re-enable Root Motion (if it was originally enabled)
                if (target.playerAnimator != null)
                {
                    target.playerAnimator.applyRootMotion = true;
                }
            }
        }
    }
    
    [PunRPC]
    private void RPC_EnterSpectator()
    {
        currentState = PlayerState.Spectating;
        
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
        
        if (photonView.IsMine)
        {
            if (playerUI != null)
            {
                playerUI.SetActive(false);
            }
            
            if (spectatorCamera != null)
            {
                spectatorCamera.StartSpectating();
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

                if (horizontalVelocity.sqrMagnitude < 0.1f)
                {
                    horizontalVelocity = Vector3.zero;
                }
            }
            else
            {
                horizontalVelocity = targetVelocity;
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
        if (infiniteStamina)
        {
            currentStamina = maxStamina;
            return;
        }

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
        bool jumpPressed = inputHandler.jumpInput && !lastJumpInput;
        lastJumpInput = inputHandler.jumpInput;

        if (jumpPressed && isGrounded && (infiniteStamina || currentStamina >= jumpStaminaCost))
        {
            // Record if player was sprinting when jump started
            jumpedWhileSprinting = inputHandler.sprintInput && inputHandler.movementInput.sqrMagnitude > INPUT_THRESHOLD * INPUT_THRESHOLD;
            
            // Set jump flag immediately - this is what triggers the animation
            isJumping = true;
            
            velocity.y = Mathf.Sqrt(jumpForce * JUMP_GRAVITY_MULTIPLIER * Mathf.Abs(gravity));
            if (!infiniteStamina)
            {
                currentStamina -= jumpStaminaCost;
                currentStamina = Mathf.Max(0f, currentStamina);
                lastSprintTime = Time.time;
            }
        }
        
        // Reset jump flag when landed (grounded and not going up)
        if (isGrounded && velocity.y <= 0 && isJumping)
        {
            isJumping = false;
            jumpedWhileSprinting = false;
        }
    }

    private void HandleInteraction()
    {
        if (inputHandler.interactInput && !lastInteractState)
        {
            if (Time.time >= lastInteractionTime + INTERACTION_COOLDOWN)
            {
                // ToggleMask();
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

    // Toggle player's walk/sprint speed between normal and `toggleSpeed`.
    private void ToggleSpeed()
    {
        speedToggleActive = !speedToggleActive;
        if (speedToggleActive)
        {
            savedWalkSpeed = walkSpeed;
            savedSprintSpeed = sprintSpeed;
            walkSpeed = toggleSpeed;
            sprintSpeed = toggleSpeed;
        }
        else
        {
            walkSpeed = savedWalkSpeed;
            sprintSpeed = savedSprintSpeed;
        }
    }

    void ToggleDiaryState()
    {
        isDiaryOpen = !isDiaryOpen;

        if (diaryUI != null)
        {
            diaryUI.ToggleDiary(isDiaryOpen, collectedPageIDs);
        }

        if (isDiaryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Call this function when you pick up a paper
    public void CollectPage(int pageID)
    {
        if (!collectedPageIDs.Contains(pageID))
        {
            collectedPageIDs.Add(pageID);
            UnityEngine.Debug.Log($"Added Page {pageID} to Diary.");
        }
    }

    private void OnGUI()
    {
        if (currentState == PlayerState.Spectating)
            return;

        if (currentGraveyardMinigame != null && currentGraveyardMinigame.IsMinigameActive())
        {
            int progress = currentGraveyardMinigame.GetCurrentProgress();
            int total = currentGraveyardMinigame.GetTotalGravestones();
            string nextName = currentGraveyardMinigame.GetNextExpectedName();
            
            GUI.Box(new Rect(Screen.width / 2 - 150, 20, 300, 60), "");
            GUI.Label(new Rect(Screen.width / 2 - 140, 30, 280, 20), $"Cemitério: {progress}/{total}");
            GUI.Label(new Rect(Screen.width / 2 - 140, 50, 280, 20), $"Próximo: {nextName}");
        }

        if (currentGraveyardMinigame != null && currentGraveyardMinigame.IsMinigameCompleted())
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, 100, 200, 30), "MINIGAME COMPLETO!");
        }
            
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

        if (currentGenerator != null)
        {
            float progress = generatorProgress / currentGenerator.timeToTurnOn;
            GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height - 100, 200, 30), $"Powering up... {progress * 100:F0}%");
        }
    }

}
