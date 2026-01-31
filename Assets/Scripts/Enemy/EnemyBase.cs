using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections.Generic;
using Photon.Pun;

public class EnemyBase : MonoBehaviourPunCallbacks, IPunObservable
{
    public enum EnemyState { Teleporting, Observing, Patrolling, Chasing, Sabotaging }

    [Header("Attack")]
    [SerializeField] private float attackRange = 2.5f;

    [Header("Players")]
    private const int maxPlayers = 2; // Maximum expected players
    private List<Transform> Players = new List<Transform>();
    private List<PlayerController> playerControllers = new List<PlayerController>();

    [Header("Detection")]
    [SerializeField] private float observeRange = 80f; // Range to start observing player
    [SerializeField] private float patrolSuspicionRange = 60f; // Range to gain suspicion during patrol (should be <= observeRange)
    [SerializeField] private float suspicionToPatrol = 0.4f;
    [SerializeField] private float suspicionToChase = 1.0f;

    [Header("Teleporting")]
    [SerializeField] private float teleportInterval = 15f;
    [SerializeField] private float teleportMinRange = 60f; // Minimum distance from player
    [SerializeField] private float teleportMaxRange = 200f; // Maximum distance from player - teleports randomly within this radius

    [Header("Suspicion")]
    [SerializeField] private float observeGainRateClose = 0.3f; // Suspicion gain when close (0-40m)
    [SerializeField] private float observeGainRateFar = 0.1f; // Suspicion gain when far (40-80m)
    [SerializeField] private float decayRate = 0.1f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 3f;
    [SerializeField] private float chaseSpeed = 6f;
    [SerializeField] private float patrolRadius = 40f; // Larger patrol radius for forest

    [Header("Sabotage")]
    [SerializeField] private float sabotageInterval = 30f; // Wait this long before breaking another
    [SerializeField] private float sabotageWalkSpeed = 3.5f;
    [SerializeField] private float sabotageMinTeleportRange = 100f; // Min distance from generator to teleport
    [SerializeField] private float sabotageMaxTeleportRange = 150f; // Max distance from generator to teleport
    private List<PowerGenerator> allGenerators = new List<PowerGenerator>();
    private PowerGenerator currentTargetGen;
    private float sabotageTimer;

    [Header("UI")]
    [SerializeField] private SuspicionSystem suspicionSystem; // Drag your fill Image component here
    private PhotonView pv;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    [Header("Line of Sight")]
    [SerializeField] private LayerMask obstacleMask = -1; // Layers that block line of sight (walls, buildings, etc.)
    [SerializeField] private float eyeHeight = 1.7f; // Height from which enemy checks line of sight

    private EnemyState state = EnemyState.Teleporting;
    private NavMeshAgent agent;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    private int speedHash;

    private float[] suspicionLevels;
    private float teleportTimer;
    private Transform target;
    private List<Vector3> patrolPoints = new List<Vector3>();
    private int patrolIndex;

    private void FindAllPlayers()
    {
        Players.Clear();
        playerControllers.Clear();

        // Find all PlayerController components in the scene
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        
        foreach (PlayerController pc in allPlayers)
        {
            // Only add if it has a PhotonView and is a real player instance
            PhotonView pv = pc.GetComponent<PhotonView>();
            if (pv != null)
            {
                Players.Add(pc.transform);
                playerControllers.Add(pc);
            }
            else
            {
                Debug.LogWarning($"Player {pc.name} has no PhotonView!");
            }
        }

        // Resize suspicion array to match player count
        if (suspicionLevels == null || suspicionLevels.Length != Players.Count)
        {
            suspicionLevels = new float[Players.Count];
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
    if (stream.IsWriting)
    {
        // Master Client - send enemy data
        stream.SendNext(transform.position);
        stream.SendNext(transform.rotation);
        stream.SendNext((int)state);

        // Send current animation speed so remote clients can mirror animations
        float animSpeed = (agent != null) ? agent.velocity.magnitude : 0f;
        stream.SendNext(animSpeed);
        
        // Send suspicion levels count first, then the values
        int count = (suspicionLevels != null) ? suspicionLevels.Length : 0;
        stream.SendNext(count);
        
        if (suspicionLevels != null)
        {
            for (int i = 0; i < suspicionLevels.Length; i++)
            {
                stream.SendNext(suspicionLevels[i]);
            }
        }
    }
    else
    {
        // Non-master clients - receive enemy data
        Vector3 networkPos = (Vector3)stream.ReceiveNext();
        Quaternion networkRot = (Quaternion)stream.ReceiveNext();
        int stateInt = (int)stream.ReceiveNext();
        float networkAnimSpeed = (float)stream.ReceiveNext();
        
        // Smoothly interpolate position
        if (agent != null && agent.enabled)
        {
            agent.Warp(networkPos);
            transform.rotation = networkRot;
        }
        else
        {
            transform.position = networkPos;
            transform.rotation = networkRot;
        }
        
        state = (EnemyState)stateInt;

        // Apply animation speed on remote clients
        if (animator != null)
        {
            animator.SetFloat(speedHash, networkAnimSpeed);
        }
        
        // Receive suspicion levels - first get the count
        int count = (int)stream.ReceiveNext();
        
        // Ensure array is initialized to the correct size
        if (suspicionLevels == null || suspicionLevels.Length != count)
        {
            suspicionLevels = new float[count];
        }
        
        // Read exactly the number of values that were sent
        for (int i = 0; i < count; i++)
        {
            suspicionLevels[i] = (float)stream.ReceiveNext();
        }
    }
}
    void Awake()
    {
        pv = GetComponent<PhotonView>();
        agent = GetComponent<NavMeshAgent>();

        // Configure agent to prevent sliding
        if (agent != null)
        {
            agent.enabled = true;
            agent.angularSpeed = 200f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;
            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.updateUpAxis = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        // Setup animator
        if (animator == null) animator = GetComponentInChildren<Animator>();
        speedHash = Animator.StringToHash("Speed");
        if (animator == null && showDebug)
        {
            Debug.LogWarning("Enemy has no Animator component; animations won't play.");
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void Start()
    {
        // Find players after scene is fully loaded
        FindAllPlayers();

        allGenerators.AddRange(FindObjectsOfType<PowerGenerator>());

        // Refresh player list after a short delay (in case players spawn late)
        Invoke(nameof(FindAllPlayers), 1f);
    }

    void Update()
    {
        // Only Master Client controls the enemy AI
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            return; // Other clients just see the synced position/rotation
        }

        if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
        {
            FindAllPlayers();
        }

        if (Players == null || Players.Count == 0) return;
        if (suspicionLevels.Length != Players.Count) suspicionLevels = new float[Players.Count];

        DecaySuspicion();

        switch (state)
        {
            case EnemyState.Teleporting: UpdateTeleporting(); break;
            case EnemyState.Observing: UpdateObserving(); break;
            case EnemyState.Patrolling: UpdatePatrolling(); break;
            case EnemyState.Chasing: UpdateChasing(); break;
            case EnemyState.Sabotaging: UpdateSabotaging(); break;
        }

        UpdateSuspicionUI();

        // Update animation parameter from agent velocity (master client authoritative)
        float currentAgentSpeed = (agent != null) ? agent.velocity.magnitude : 0f;
        if (animator != null)
        {
            animator.SetFloat(speedHash, currentAgentSpeed);
        }
    }

    void UpdateSuspicionUI()
    {
        if (suspicionSystem != null)
        {
            float maxSus = GetMaxSuspicion();
            // Fill amount goes from 0 to 1 based on suspicion threshold
            suspicionSystem.suspicionLevel = maxSus / suspicionToChase;
        }
    }

    void LateUpdate()
    {
        // Clamp agent to NavMesh to prevent sliding off
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // Ensure agent stays on NavMesh
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(transform.position, hit.position) > 0.1f)
                {
                    transform.position = hit.position;
                }
            }
        }
    }


    // ===== TELEPORTING =====
    void UpdateTeleporting()
    {
        if (agent == null)
            return;

        agent.isStopped = true;

        Transform nearest = GetNearestInRange(observeRange);
        if (nearest != null)
        {
            state = EnemyState.Observing;
            target = nearest;
            return;
        }

        sabotageTimer += Time.deltaTime;
        if (sabotageTimer >= sabotageInterval)
        {
            PowerGenerator targetGen = GetActiveGenerator();
            
            if (targetGen != null)
            {
                currentTargetGen = targetGen;
                
                // Teleport near the generator instead of directly to it
                TeleportNearGenerator(targetGen);
                
                ChangeState(EnemyState.Sabotaging); 
                return;
            }
        }

        teleportTimer += Time.deltaTime;
        if (teleportTimer >= teleportInterval)
        {
            Teleport();
            teleportTimer = 0f;
        }
    }

    public float setSpeed(float newSpeed)
    {
        float tempSpeed = agent.speed;
        agent.speed = newSpeed;
        return tempSpeed;
    }

    public Vector3 SetDestination(Vector3 newDest)
    {
        Vector3 tempDest = agent.destination;
        agent.destination = newDest;
        return tempDest;
    }

    void Teleport()
    {
        if (agent == null)
        {
            Debug.LogError("Agent is null in Teleport!");
            return;
        }

        if (!agent.enabled)
        {
            Debug.LogWarning("Agent is disabled! Enabling it...");
            agent.enabled = true;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Agent is not on NavMesh! Position: " + transform.position);
            return;
        }

        if (Players == null || Players.Count == 0)
        {
            Debug.LogError("No players found for teleporting!");
            return;
        }

        // Find a valid player
        Transform randomPlayer = null;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i] != null)
            {
                randomPlayer = Players[i];
                break;
            }
        }

        if (randomPlayer == null)
        {
            Debug.LogWarning("No valid player transforms found!");
            return;
        }

        // Teleport randomly within radius around player (no bias, pure random)
        float randomAngle = Random.Range(0f, 360f);
        float randomDistance = Random.Range(teleportMinRange, teleportMaxRange);

        Vector3 offset = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward * randomDistance;
        Vector3 targetPoint = randomPlayer.position + offset;

        // Try multiple times with different search parameters
        bool teleported = false;
        int navMaskAll = -1; // Use -1 instead of NavMesh.AllAreas for better compatibility

        for (int attempt = 0; attempt < 5 && !teleported; attempt++)
        {
            // Use the calculated target point
            Vector3 pos = targetPoint;

            // Sample from player's height instead of adding 50
            pos.y = randomPlayer.position.y + 10f;

            // Try progressively larger search radii
            float[] searchRadii = { 50f, 100f, 200f };
            foreach (float searchRadius in searchRadii)
            {
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, searchRadius, navMaskAll))
                {
                    // Add bigger height offset to prevent spawning inside floor
                    Vector3 warpPosition = hit.position;
                    warpPosition.y += 2f; // Lift 2 meters above NavMesh surface (increased from 1m)

                    if (agent.Warp(warpPosition))
                    {
                        // Force re-enable agent after warp
                        agent.enabled = true;
                        agent.isStopped = false;

                        teleported = true;
                        break;
                    }
                    else
                    {
                        Debug.LogError("Warp FAILED even though NavMesh was found!");
                    }
                }
                else
                {
                    Debug.LogWarning($"No NavMesh found with {searchRadius}m search radius");
                }
            }
        }

        if (!teleported)
            Debug.LogError($"<color=red>TELEPORT COMPLETELY FAILED after 5 attempts! NavMesh might not be baked on terrain. Enemy staying at: {transform.position}</color>");
    }

    // ===== OBSERVING =====
    void UpdateObserving()
    {
        agent.isStopped = true;

        if (target == null)
        {
            state = EnemyState.Teleporting;
            return;
        }

        // Check if target is still alive
        PlayerController targetController = target.GetComponent<PlayerController>();
        if (targetController != null && targetController.GetCurrentState() != PlayerState.Alive)
        {
            state = EnemyState.Teleporting;
            target = null;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > observeRange)
        {
            state = EnemyState.Teleporting;
            target = null;
            return;
        }

        // Make enemy stare directly at the player - aim for head/center
        Vector3 targetPos = target.position + Vector3.up * 1.5f; // Look at player's head height
        Vector3 dir = (targetPos - transform.position).normalized;

        // Fast, direct rotation to stare intensely
        Quaternion targetRotation = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);

        // Check line of sight - only gain suspicion if we can actually see the player
        bool canSeePlayer = HasLineOfSight(target);
        
        if (!canSeePlayer)
        {
            // Can't see player through walls - decay suspicion faster
            int idx = Players.IndexOf(target);
            if (idx >= 0)
            {
                suspicionLevels[idx] = Mathf.Max(0f, suspicionLevels[idx] - decayRate * 2f * Time.deltaTime);
                
                if (suspicionLevels[idx] < suspicionToPatrol * 0.3f)
                {
                    // Lost sight and suspicion dropped - go back to teleporting
                    state = EnemyState.Teleporting;
                    target = null;
                }
            }
            return;
        }

        int idx2 = Players.IndexOf(target);
        if (idx2 >= 0)
        {
            // Apply mask effect - mask reduces suspicion gain
            float maskEffect = GetPlayerMask(target);

            // Distance-based suspicion gain - slower from far away, faster up close
            float distance = Vector3.Distance(transform.position, target.position);
            float distanceRatio = distance / observeRange; // 0 = very close, 1 = at max range
            float distanceBasedGain = Mathf.Lerp(observeGainRateClose, observeGainRateFar, distanceRatio);

            float effectiveSuspicionGain = distanceBasedGain * maskEffect * Time.deltaTime;
            
            // Debug log suspicion gain
            if (Time.frameCount % 60 == 0 && showDebug) // Every second
            {
                Debug.Log($"<color=yellow>Observing {target.name} (idx {idx2}): dist={distance:F1}m, suspicion={suspicionLevels[idx2]:F2}, gain={effectiveSuspicionGain:F4}/frame, maskEffect={maskEffect:F2}, LOS=TRUE</color>");
            }

            suspicionLevels[idx2] += effectiveSuspicionGain;
            suspicionLevels[idx2] = Mathf.Clamp01(suspicionLevels[idx2]);

            if (suspicionLevels[idx2] >= suspicionToPatrol)
            {
                state = EnemyState.Patrolling;
                GeneratePatrol(target.position);
            }
        }
    }

    // ===== PATROLLING =====
    void UpdatePatrolling()
    {
        // Ensure agent is properly configured
        if (!agent.isOnNavMesh)
        {
            Debug.LogError($"Agent not on NavMesh during patrol! Position: {transform.position}");
            state = EnemyState.Teleporting;
            return;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;

        Transform mostSus = GetMostSuspicious();
        if (mostSus == null)
        {
            state = EnemyState.Teleporting;
            return;
        }

        float maxSus = GetMaxSuspicion();

        if (maxSus < suspicionToPatrol * 0.6f)
        {
            state = EnemyState.Observing;
            target = mostSus;
            return;
        }

        if (maxSus >= suspicionToChase)
        {
            state = EnemyState.Chasing;
            patrolPoints.Clear();
            return;
        }

        // Gain suspicion while patrolling if player is nearby AND ALIVE AND visible (line of sight)
        float distToPlayer = Vector3.Distance(transform.position, mostSus.position);
        if (distToPlayer <= patrolSuspicionRange)
        {
            int playerIdx = Players.IndexOf(mostSus);
            if (playerIdx >= 0)
            {
                PlayerController pc = mostSus.GetComponent<PlayerController>();
                if (pc != null && pc.GetCurrentState() == PlayerState.Alive)
                {
                    // Only gain suspicion if we can see the player
                    if (HasLineOfSight(mostSus))
                    {
                        // Apply mask effect and distance-based gain
                        float maskEffect = GetPlayerMask(mostSus);

                        // Distance-based gain during patrol
                        float distanceRatio = distToPlayer / patrolSuspicionRange;
                        float distanceBasedGain = Mathf.Lerp(observeGainRateClose, observeGainRateFar, distanceRatio);
                        float patrolSuspicionGain = distanceBasedGain * 0.5f * maskEffect; // Half rate during patrol

                        suspicionLevels[playerIdx] += patrolSuspicionGain * Time.deltaTime;
                        suspicionLevels[playerIdx] = Mathf.Clamp01(suspicionLevels[playerIdx]);
                    }
                }
            }
        }

        if (patrolPoints.Count == 0)
        {
            GeneratePatrol(mostSus.position);
        }

        if (patrolPoints.Count > 0)
        {
            agent.SetDestination(patrolPoints[patrolIndex]);

            if (Time.frameCount % 60 == 0 && false) // Log every 60 frames
            {
                Debug.Log($"Patrolling - Point {patrolIndex}/{patrolPoints.Count}, Distance: {agent.remainingDistance:F1}m, Speed: {agent.speed}");
            }

            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                if (patrolIndex == 0) GeneratePatrol(mostSus.position);
            }
        }
        else
        {
            Debug.LogWarning("No patrol points generated! Returning to teleporting.");
            state = EnemyState.Teleporting;
        }
    }
    // ===== SABOTAGE =====
    void UpdateSabotaging()
    {
        if (currentTargetGen == null || !currentTargetGen.IsOn)
        {
            state = EnemyState.Teleporting;
            sabotageTimer = 0f;
            return;
        }

        Transform nearest = GetNearestInRange(observeRange);
        if (nearest != null)
        {
            state = EnemyState.Observing;
            target = nearest;
            return;
        }

        agent.isStopped = false;
        agent.speed = sabotageWalkSpeed;
        agent.SetDestination(currentTargetGen.transform.position);

        if (Vector3.Distance(transform.position, currentTargetGen.transform.position) < 3.0f)
        {
            currentTargetGen.EnemySabotage();
            sabotageTimer = 0f;
            state = EnemyState.Teleporting;
        }
    }
    
    void TeleportNearGenerator(PowerGenerator generator)
    {
        if (agent == null || !agent.isOnNavMesh) return;
        
        Vector3 generatorPos = generator.transform.position;
        
        // Random angle and distance within sabotage teleport range
        float randomAngle = Random.Range(0f, 360f);
        float randomDistance = Random.Range(sabotageMinTeleportRange, sabotageMaxTeleportRange);
        
        Vector3 offset = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward * randomDistance;
        Vector3 targetPoint = generatorPos + offset;
        targetPoint.y = generatorPos.y + 10f;
        
        int navMaskAll = -1;
        
        // Try to find valid NavMesh position near generator
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float[] searchRadii = { 50f, 100f, 200f };
            foreach (float searchRadius in searchRadii)
            {
                if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, searchRadius, navMaskAll))
                {
                    Vector3 warpPosition = hit.position;
                    warpPosition.y += 2f;
                    
                    if (agent.Warp(warpPosition))
                    {
                        agent.enabled = true;
                        agent.isStopped = false;
                        return;
                    }
                }
            }
            
            // Try different position if failed
            randomAngle = Random.Range(0f, 360f);
            randomDistance = Random.Range(sabotageMinTeleportRange, sabotageMaxTeleportRange);
            offset = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward * randomDistance;
            targetPoint = generatorPos + offset;
            targetPoint.y = generatorPos.y + 10f;
        }
    }

    PowerGenerator GetActiveGenerator()
    {
        foreach (var gen in allGenerators)
            if (gen != null && gen.IsOn) return gen;
        return null;
    }

    void GeneratePatrol(Vector3 center)
    {
        patrolPoints.Clear();

        int navMaskAll = -1;

        for (int i = 0; i < 6; i++)
        {
            float angle = (360f / 6f) * i + Random.Range(-20f, 20f);
            float dist = patrolRadius + Random.Range(-5f, 5f);
            Vector3 pos = center + Quaternion.Euler(0, angle, 0) * Vector3.forward * dist;
            pos.y = center.y + 5f; // Sample from player height

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 50f, navMaskAll))
            {
                patrolPoints.Add(hit.position);
            }
        }

        patrolIndex = 0;
    }

    // ===== CHASING =====
    void UpdateChasing()
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;

        Transform mostSus = GetMostSuspicious();
        if (mostSus == null)
        {
            state = EnemyState.Teleporting;
            return;
        }

        if (Time.frameCount % 60 == 0 && false) // Log every second
        {
            Debug.Log($"Chasing {mostSus.name} at distance {Vector3.Distance(transform.position, mostSus.position):F1}m");
        }

        agent.SetDestination(mostSus.position);

        float distToPlayer = Vector3.Distance(transform.position, mostSus.position);
        if (distToPlayer <= attackRange)
        {
            if (mostSus == null) return;
            PlayerController playerController = mostSus.GetComponent<PlayerController>();
            
            Debug.Log($"Enemy in attack range ({distToPlayer:F1}m <= {attackRange}m) of {mostSus.name}");
            
            if (playerController != null)
            {
                PlayerState pState = playerController.GetCurrentState();
                Debug.Log($"Player {mostSus.name} state: {pState}");
                
                if (pState == PlayerState.Alive)
                {
                    // Use RPC to kill player across network
                    PhotonView targetPhotonView = playerController.GetComponent<PhotonView>();
                    if (targetPhotonView != null)
                    {
                        Debug.Log($"Calling RPC_KillPlayer on {mostSus.name} (ViewID: {targetPhotonView.ViewID})");
                        targetPhotonView.RPC(nameof(PlayerController.RPC_KillPlayer), RpcTarget.All);
                        Debug.Log($"Enemy killed player {playerController.name}");
                    }
                    else
                    {
                        Debug.LogError($"Player {mostSus.name} has no PhotonView!");
                    }
                }
            }
            else
            {
                Debug.LogError($"Player {mostSus.name} has no PlayerController!");
            }
        }

        if (GetMaxSuspicion() < suspicionToChase * 0.7f)
        {
            state = EnemyState.Patrolling;
            target = mostSus;
            GeneratePatrol(mostSus.position);
        }
    }

    // ===== HELPERS =====
    
    /// <summary>
    /// Checks if the enemy has a clear line of sight to the target (no walls/obstacles)
    /// </summary>
    bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = target.position + Vector3.up * 1.5f; // Aim for player's upper body/head
        Vector3 direction = targetPosition - eyePosition;
        float distance = direction.magnitude;

        // Raycast to check for obstacles
        if (Physics.Raycast(eyePosition, direction.normalized, out RaycastHit hit, distance, obstacleMask))
        {
            // Check if we hit the player or an obstacle
            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                // Hit the player directly - we can see them
                return true;
            }
            else
            {
                // Hit an obstacle (wall, building, etc.) - can't see player
                if (showDebug && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"<color=orange>Line of sight blocked by {hit.collider.name} at {hit.point}</color>");
                }
                return false;
            }
        }

        // Nothing blocking the view
        return true;
    }
    
    void DecaySuspicion()
    {
        // Decay suspicion for dead/downed players OR players out of range
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (Players[i] == null)
            {
                suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * Time.deltaTime);
                continue;
            }

            PlayerController pc = Players[i].GetComponent<PlayerController>();
            bool isAlive = pc != null && pc.GetCurrentState() == PlayerState.Alive;

            // If player is dead/downed, decay their suspicion rapidly
            if (!isAlive)
            {
                suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * 3f * Time.deltaTime); // 3x faster decay for downed players
                continue;
            }

            // Normal decay logic for alive players
            if (state == EnemyState.Observing || state == EnemyState.Patrolling || state == EnemyState.Chasing)
            {
                float distance = Vector3.Distance(transform.position, Players[i].position);
                bool playerInRange = distance <= observeRange;

                // Only decay if player is far away
                if (!playerInRange)
                {
                    suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * Time.deltaTime);
                }
            }
            else if (state == EnemyState.Teleporting)
            {
                // Normal decay when teleporting (not engaged)
                suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * Time.deltaTime);
            }
        }
    }

    Transform GetNearestInRange(float range)
    {
        Transform nearest = null;
        float minDist = range;
        
        if (Time.frameCount % 120 == 0 && showDebug) // Every 2 seconds
        {
            Debug.Log($"<color=magenta>GetNearestInRange: Checking {Players.Count} players within {range}m</color>");
        }
        
        foreach (Transform p in Players)
        {
            if (p == null) continue;

            // Only consider alive players
            PlayerController pc = p.GetComponent<PlayerController>();
            if (pc != null && pc.GetCurrentState() != PlayerState.Alive)
                continue;

            float d = Vector3.Distance(transform.position, p.position);
            
            // Check if within range AND has line of sight
            if (d < minDist)
            {
                // Only detect if we can see the player (no walls blocking)
                if (HasLineOfSight(p))
                {
                    if (Time.frameCount % 120 == 0 && showDebug)
                    {
                        Debug.Log($"<color=magenta>  Player {p.name}: distance={d:F1}m, alive=true, LOS=TRUE</color>");
                    }
                    minDist = d;
                    nearest = p;
                }
                else if (Time.frameCount % 120 == 0 && showDebug)
                {
                    Debug.Log($"<color=magenta>  Player {p.name}: distance={d:F1}m, alive=true, LOS=FALSE (blocked by obstacle)</color>");
                }
            }
        }
        
        if (Time.frameCount % 120 == 0 && nearest != null && showDebug)
        {
            Debug.Log($"<color=magenta>Nearest VISIBLE player: {nearest.name} at {minDist:F1}m</color>");
        }
        
        return nearest;
    }
    private void ChangeState(EnemyState newState)
    {
        state = newState;

        if (PhotonNetwork.IsConnected && pv != null && PhotonNetwork.IsMasterClient)
        {
            pv.RPC(nameof(RPC_SyncState), RpcTarget.Others, (int)newState);
        }
    }

    [PunRPC]
    private void RPC_SyncState(int stateIndex)
    {
        state = (EnemyState)stateIndex;
    }
    Transform GetMostSuspicious()
    {
        int idx = -1;
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (Players[i] == null) continue;

            PlayerController playerController = Players[i].GetComponent<PlayerController>();
            if (playerController != null && playerController.GetCurrentState() != PlayerState.Alive)
            {
                continue; // Skip downed/dead players
            }

            if (suspicionLevels[i] > max)
            {
                max = suspicionLevels[i];
                idx = i;
            }
        }
        return (idx >= 0 && idx < Players.Count) ? Players[idx] : null;
    }

    float GetMaxSuspicion()
    {
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (Players[i] == null) continue;

            // Only count suspicion from alive players
            PlayerController pc = Players[i].GetComponent<PlayerController>();
            if (pc != null && pc.GetCurrentState() != PlayerState.Alive)
                continue;

            if (suspicionLevels[i] > max)
                max = suspicionLevels[i];
        }
        return max;
    }

    public void IncreaseSuspicion(int playerIndex, float amount)
    {
        if (playerIndex >= 0 && playerIndex < suspicionLevels.Length)
            suspicionLevels[playerIndex] = Mathf.Clamp01(suspicionLevels[playerIndex] + amount);
    }

    float GetPlayerMask(Transform player)
    {
        if (player == null) return 1f;

        // Try to get from cached list first
        int idx = Players.IndexOf(player);
        if (idx >= 0 && idx < playerControllers.Count)
        {
            PlayerController pc = playerControllers[idx];
            if (pc != null)
            {
                PlayerMask maskComponent = pc.GetComponent<PlayerMask>();
                if (maskComponent != null)
                {
                    return maskComponent.GetMaskEffect();
                }
            }
        }

        // Fallback
        PlayerMask mask = player.GetComponent<PlayerMask>();
        if (mask != null)
        {
            return mask.GetMaskEffect();
        }

        return 1f;
    }

    // ===== DEBUG =====
    void OnDrawGizmos()
    {
        if (!showDebug) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, observeRange);

        // Draw line of sight rays to all players
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        foreach (Transform player in Players)
        {
            if (player == null) continue;
            
            Vector3 targetPos = player.position + Vector3.up * 1.5f;
            bool hasLOS = HasLineOfSight(player);
            
            // Green line if can see, red if blocked
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(eyePos, targetPos);
        }

        if (target != null)
        {
            bool hasLOS = HasLineOfSight(target);
            Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
            Gizmos.DrawLine(transform.position, target.position);
        }

        Gizmos.color = Color.green;
        foreach (Vector3 p in patrolPoints)
            Gizmos.DrawSphere(p, 0.5f);
    }

    void OnGUI()
    {
        if (!showDebug) return;

        float maxSus = GetMaxSuspicion();
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 300, 20), $"State: {state}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Suspicion: {maxSus:F2}");
        if (state == EnemyState.Teleporting)
            GUI.Label(new Rect(10, 70, 300, 20), $"Teleport in: {(teleportInterval - teleportTimer):F1}s");

        GUI.Box(new Rect(10, 50, 200, 15), "");

        float patrolX = 10 + 200 * (suspicionToPatrol / suspicionToChase);
        GUI.color = Color.yellow;
        GUI.Box(new Rect(patrolX - 1, 50, 2, 15), "");

        float fillWidth = 200 * (maxSus / suspicionToChase);
        GUI.color = Color.Lerp(Color.green, Color.red, maxSus / suspicionToChase);
        GUI.Box(new Rect(10, 50, fillWidth, 15), "");
    }
}