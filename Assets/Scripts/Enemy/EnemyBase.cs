using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Photon.Pun;

public class EnemyBase : MonoBehaviourPunCallbacks
{
    public enum EnemyState
    {
        Idle,
        Patrolling,
        Chasing
    }

    [Header("Base Vars")]
    [Header("AI")]
    [SerializeField] private EnemyState CurrentState = EnemyState.Idle;
    [SerializeField] private List<Transform> PatrolPoints;
    private int currentPatrolIndex = 0;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 3.5f;
    [SerializeField] private float chaseSpeed = 5.5f;

    [Header("Detection & Hunting")]
    [SerializeField] private List<Transform> Players;
    [SerializeField] private float DetectionRange = 25f;
    [SerializeField] private float SightAngle = 110f;
    [SerializeField] private float SuspicionThreshold = 1.0f;
    [SerializeField] private float SuspicionGainRate = 1.5f;
    [SerializeField] private float SuspicionDecayRate = 0.25f;
    [SerializeField] private LayerMask ObstacleMask;
    [SerializeField] private float watchPlayerDuration = 4f; // How long to stop and watch when spotting player
    [SerializeField] private float watchSuspicionGain = 0.4f; // Suspicion gain per second while watching
    [SerializeField] private float investigateDuration = 2f; // Brief pause when hearing noise
    
    [Header("Teleport Behavior")]
    [SerializeField] private float teleportCooldown = 20f;
    [SerializeField] private float jumpscareChance = 0.015f; // Rare jumpscare teleport
    [SerializeField] private float jumpscareCooldown = 45f;
    [SerializeField] private float jumpscareDistance = 5f;
    [SerializeField] private float jumpscareDuration = 2.5f;
    [SerializeField] private float teleportRadius = 35f;
    [SerializeField] private float farTeleportRadius = 80f; // Much farther when lost
    [SerializeField] private float timeBeforeFarTeleport = 25f; // Time without sighting to trigger far teleport
    [SerializeField] private float lowSuspicionThreshold = 0.3f; // Below this is considered "lost trail"
    [SerializeField] private float timeAtLowSuspicionBeforeSearch = 15f; // Time at low suspicion before searching elsewhere
    
    [Header("Debug UI (REMOVE LATER)")]
    [SerializeField] private bool showSuspicionDebugUI = true;

    [Header("Hunting Patrol")]
    [SerializeField] private float huntRadiusMin = 15f;
    [SerializeField] private float huntRadiusMax = 35f;
    [SerializeField] private int patrolPointCount = 5;
    [SerializeField] private float patrolRebuildInterval = 3f;
    [SerializeField] private float idleDuration = 1.5f;
    
    private float idleTimer = 0f;
    private Vector3 lastKnownPlayerPos;

    private float patrolRebuildTimer = 0f;
    private float teleportTimer = 0f;
    private float jumpscareTimer = 0f;
    private float timeSinceLastSighting = 0f;
    private float timeAtLowSuspicion = 0f;
    private bool isWatchingPlayer = false;
    private float watchTimer = 0f;
    private Transform watchTarget;
    private Vector3 watchLockedPosition; // Position where enemy first spotted player
    private bool isInvestigating = false;
    private float investigateTimer = 0f;
    private Vector3 investigateDirection;

    private NavMeshAgent Agent;
    private float[] suspicionLevels;



    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        EnsureSuspicionArray();
    }

    void Update()
    {
        //if (!PhotonNetwork.IsMasterClient) return;

        if (isInvestigating)
        {
            HandleInvestigating();
            return;
        }

        if (isWatchingPlayer)
        {
            HandleWatchingPlayer();
            if (isWatchingPlayer) // Still watching after handling
            {
                return;
            }
        }

        EnsureSuspicionArray();
        UpdateSuspicion();
        CheckForPlayerInSight();
        EvaluateAggro();
        
        timeSinceLastSighting += Time.deltaTime;

        switch (CurrentState)
        {
            case EnemyState.Idle:
                HandleIdleState();
                break;
            case EnemyState.Patrolling:
                HandlePatrollingState();
                break;
            case EnemyState.Chasing:
                HandleChasingState();
                break;
        }
    }

    void HandleIdleState()
    {
        SetSpeed(0f); // stop movement
        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDuration)
        {
            idleTimer = 0f;
            EnterState(EnemyState.Patrolling);
        }
    }

    void HandlePatrollingState()
    {
        SetSpeed(patrolSpeed);
        teleportTimer += Time.deltaTime;
        jumpscareTimer += Time.deltaTime;

        // Track time at low suspicion
        float maxSuspicion = GetMaxSuspicion();
        if (maxSuspicion < lowSuspicionThreshold)
        {
            timeAtLowSuspicion += Time.deltaTime;
        }
        else
        {
            timeAtLowSuspicion = 0f;
        }

        // If suspicion stays low for too long, search elsewhere
        if (timeAtLowSuspicion >= timeAtLowSuspicionBeforeSearch)
        {
            TeleportToSearchArea();
            timeAtLowSuspicion = 0f;
            return;
        }

        // Rare jumpscare teleport
        if (jumpscareTimer > jumpscareCooldown && Random.value < jumpscareChance * Time.deltaTime)
        {
            TeleportForJumpscare();
            return;
        }

        Transform target = GetClosestPlayer();
        if (target != null)
        {
            lastKnownPlayerPos = target.position;
        }

        // Rebuild patrol points around last known player position
        patrolRebuildTimer += Time.deltaTime;
        if (patrolRebuildTimer >= patrolRebuildInterval || PatrolPoints == null || PatrolPoints.Count == 0)
        {
            BuildHuntingPatrolPoints();
            patrolRebuildTimer = 0f;
            currentPatrolIndex = 0;
        }

        Patrol();

        // Teleport if too far from hunting area
        if (teleportTimer >= teleportCooldown)
        {
            TeleportToHuntingArea();
        }
    }

    void TeleportToSearchArea()
    {
        // Lost the trail - teleport far away to search
        Vector3 searchCenter = lastKnownPlayerPos != Vector3.zero ? lastKnownPlayerPos : transform.position;
        
        float angle = Mathf.PerlinNoise(Time.time * 0.1f, 0f) * 360f;
        float dist = Mathf.Lerp(farTeleportRadius * 0.7f, farTeleportRadius, Mathf.PerlinNoise(Time.time * 0.08f, 1f));
        
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
        Vector3 targetPos = searchCenter + offset;
        targetPos.y += 50f;
        
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 30f, NavMesh.AllAreas))
        {
            Agent.Warp(hit.position);
            teleportTimer = 0f;
            currentPatrolIndex = 0;
        }
    }

    void EnterState(EnemyState newState)
    {
        CurrentState = newState;

        if (newState == EnemyState.Idle)
        {
            idleTimer = 0f;
        }
        else if (newState == EnemyState.Patrolling)
        {
            patrolRebuildTimer = 0f;
        }
        else if (newState == EnemyState.Chasing)
        {
            teleportTimer = 0f;
        }
    }

    void HandleWatchingPlayer()
    {
        if (watchTarget == null)
        {
            isWatchingPlayer = false;
            watchTimer = 0f;
            return;
        }

        SetSpeed(0f);
        
        // Look at the locked position (where we first saw them), not the moving player
        Vector3 directionToWatch = (watchLockedPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToWatch);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
        
        // Check if player is still in view
        bool playerStillVisible = CanSeePlayer(watchTarget);
        
        if (playerStillVisible)
        {
            // Build suspicion while player remains visible
            int playerIndex = Players.IndexOf(watchTarget);
            if (playerIndex >= 0 && playerIndex < suspicionLevels.Length)
            {
                float distance = Vector3.Distance(transform.position, watchTarget.position);
                float distanceFactor = Mathf.Clamp01(1f - (distance / DetectionRange));
                
                // Cube the distance factor - very slow at edge
                distanceFactor = distanceFactor * distanceFactor * distanceFactor;
                
                // Mask effectiveness scales with distance
                float maskEffect = GetPlayerMask(watchTarget);
                float distanceNormalized = distance / DetectionRange;
                float maskPower = Mathf.Lerp(0.15f, 2.0f, distanceNormalized);
                float finalMaskEffect = Mathf.Pow(maskEffect, maskPower);
                
                suspicionLevels[playerIndex] += distanceFactor * finalMaskEffect * watchSuspicionGain * Time.deltaTime;
                suspicionLevels[playerIndex] = Mathf.Clamp01(suspicionLevels[playerIndex]);
                
                // Check if suspicion reached threshold during watching - immediately start chase
                if (suspicionLevels[playerIndex] >= SuspicionThreshold)
                {
                    isWatchingPlayer = false;
                    watchTimer = 0f;
                    watchTarget = null;
                    CurrentState = EnemyState.Chasing;
                    return;
                }
            }
            watchTimer = 0f; // Reset timer while player is visible
        }
        else
        {
            // Player left vision - count down to resume patrol
            watchTimer += Time.deltaTime;
            if (watchTimer >= watchPlayerDuration)
            {
                isWatchingPlayer = false;
                watchTimer = 0f;
                watchTarget = null;
            }
        }
    }

    void HandleInvestigating()
    {
        SetSpeed(0f);
        
        // Subtly look toward the noise direction
        Vector3 lookTarget = transform.position + investigateDirection;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            Quaternion.LookRotation(investigateDirection),
            Time.deltaTime * 2f
        );
        
        investigateTimer += Time.deltaTime;
        if (investigateTimer >= investigateDuration)
        {
            isInvestigating = false;
            investigateTimer = 0f;
        }
    }

    void CheckForPlayerInSight()
    {
        if (isWatchingPlayer || CurrentState == EnemyState.Chasing) return;

        Transform visiblePlayer = GetVisiblePlayer();
        if (visiblePlayer != null)
        {
            isWatchingPlayer = true;
            watchTarget = visiblePlayer;
            watchLockedPosition = visiblePlayer.position; // Lock onto this position
            watchTimer = 0f;
            lastKnownPlayerPos = visiblePlayer.position;
            timeSinceLastSighting = 0f;
        }
    }

    Transform GetVisiblePlayer()
    {
        foreach (Transform player in Players)
        {
            if (player != null && CanSeePlayer(player))
            {
                return player;
            }
        }
        return null;
    }


    void BuildHuntingPatrolPoints()
    {
        if (PatrolPoints == null)
            PatrolPoints = new List<Transform>();

        ClearPatrolPoints();

        // Center patrol between enemy position and last known player position
        Vector3 enemyPos = transform.position;
        Vector3 playerPos = lastKnownPlayerPos != Vector3.zero ? lastKnownPlayerPos : enemyPos;
        
        // Higher suspicion = closer patrol to player (0.2 to 0.7 bias range)
        float maxSuspicion = GetMaxSuspicion();
        float suspicionBias = Mathf.Lerp(0.2f, 0.7f, maxSuspicion);
        Vector3 huntCenter = Vector3.Lerp(enemyPos, playerPos, suspicionBias);

        for (int i = 0; i < patrolPointCount; i++)
        {
            float noiseAngle = Mathf.PerlinNoise(i * 0.5f + Time.time * 0.1f, i * 0.3f);
            float noiseRadius = Mathf.PerlinNoise(i * 0.7f, Time.time * 0.08f + i);
            
            float angle = noiseAngle * 360f;
            float radius = Mathf.Lerp(huntRadiusMin, huntRadiusMax, noiseRadius);

            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            Vector3 rawPos = huntCenter + offset;
            
            // Sample from above for terrain
            rawPos.y += 20f;

            if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 25f, NavMesh.AllAreas))
            {
                var go = new GameObject("HuntPoint_Runtime");
                go.transform.position = hit.position;
                PatrolPoints.Add(go.transform);
            }
        }
    }

    void ClearPatrolPoints()
    {
        if (PatrolPoints == null || PatrolPoints.Count == 0) return;

        for (int i = 0; i < PatrolPoints.Count; i++)
        {
            if (PatrolPoints[i] != null)
            {
                Destroy(PatrolPoints[i].gameObject);
            }
        }
        PatrolPoints.Clear();
    }

    void Patrol()
    {
        if (PatrolPoints.Count == 0) return;
        SetDestination(PatrolPoints[currentPatrolIndex].position);
        if (Agent.remainingDistance < 0.5f)
            currentPatrolIndex = (currentPatrolIndex + 1) % PatrolPoints.Count;
    }

    void TeleportForJumpscare()
    {
        Transform target = GetClosestPlayer();
        if (target == null) return;

        Vector3 dir = (transform.position - target.position).normalized;
        Vector3 teleportPos = target.position + dir * jumpscareDistance;
        
        // Account for terrain height
        teleportPos.y += 10f;
        
        if (NavMesh.SamplePosition(teleportPos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            Agent.Warp(hit.position);
            transform.LookAt(target.position);
            isWatchingPlayer = true;
            watchTarget = target;
            watchTimer = 0f;
            jumpscareTimer = 0f;
            teleportTimer = 0f;
            timeSinceLastSighting = 0f;
        }
    }

    void TeleportToHuntingArea()
    {
        bool lostPlayer = timeSinceLastSighting > timeBeforeFarTeleport;
        
        Vector3 huntPos = lastKnownPlayerPos != Vector3.zero ? lastKnownPlayerPos : transform.position;
        
        float angle = Mathf.PerlinNoise(Time.time * 0.1f, 0f) * 360f;
        float dist;
        float searchRadius;
        
        if (lostPlayer)
        {
            // Go much farther away when lost
            dist = Mathf.Lerp(farTeleportRadius * 0.7f, farTeleportRadius, Mathf.PerlinNoise(Time.time * 0.08f, 1f));
            searchRadius = 30f; // Larger search radius for rough terrain
        }
        else
        {
            // Stay in hunting range
            dist = Mathf.Lerp(huntRadiusMin, huntRadiusMax, Mathf.PerlinNoise(Time.time * 0.08f, 1f));
            searchRadius = 15f;
        }
        
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
        Vector3 targetPos = huntPos + offset;
        
        // Account for terrain height - sample from high above down to terrain
        targetPos.y += 50f;
        
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            Agent.Warp(hit.position);
            teleportTimer = 0f;
            currentPatrolIndex = 0;
            
            if (lostPlayer)
            {
                timeSinceLastSighting = 0f; // Reset after far teleport
            }
        }
    }

    // Handles chasing logic
    void HandleChasingState()
    {
        SetSpeed(chaseSpeed);
        Transform target = GetMostSuspiciousPlayer();
        if (target != null)
        {
            SetDestination(target.position);
        }
    }
    
    public float SetSpeed(float newSpeed)
    {
        float tempSpeed = Agent.speed;
        Agent.speed = newSpeed;
        return tempSpeed;
    }

    public Vector3 SetDestination(Vector3 newDest)
    {
        Vector3 tempDest = Agent.destination;
        Agent.destination = newDest;
        return tempDest;
    }

    /// <summary>
    /// Increases suspicion for a specific player. Use this for external systems like audio detection.
    /// </summary>
    /// <param name="player">The player transform to increase suspicion for</param>
    /// <param name="amount">Amount to add (0-1 range recommended, will be clamped)</param>
    public void IncreaseSuspicion(int playerIndex, float amount)
    {
        if (Players == null) return;
        
        if (playerIndex >= 0 && playerIndex < suspicionLevels.Length)
            suspicionLevels[playerIndex] = Mathf.Clamp01(suspicionLevels[playerIndex] + amount);
    }

    /// <summary>
    /// Gets the current suspicion level for a specific player (0-1 range)
    /// </summary>
    public float GetSuspicionForPlayer(Transform player)
    {
        if (player == null || Players == null) return 0f;
        
        int playerIndex = Players.IndexOf(player);
        if (playerIndex >= 0 && playerIndex < suspicionLevels.Length)
        {
            return suspicionLevels[playerIndex];
        }
        return 0f;
    }

    void UpdateSuspicion()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Transform player = Players[i];
            if (player == null) continue;

            float suspicionChange = 0f;
            if (CanSeePlayer(player))
            {
                float distance = Vector3.Distance(transform.position, player.position);
                float distanceFactor = Mathf.Clamp01(1f - (distance / DetectionRange));
                
                // Cube the distance factor - very slow at edge, reasonable up close
                distanceFactor = distanceFactor * distanceFactor * distanceFactor;
                
                // Mask effectiveness scales with distance - powerful far away, weak up close
                float maskEffect = GetPlayerMask(player);
                float distanceNormalized = distance / DetectionRange;
                float maskPower = Mathf.Lerp(0.15f, 2.0f, distanceNormalized);
                float finalMaskEffect = Mathf.Pow(maskEffect, maskPower);
                
                suspicionChange = distanceFactor * finalMaskEffect * SuspicionGainRate * Time.deltaTime;
            }
            else
            {
                // Decay suspicion when not visible
                suspicionChange = -SuspicionDecayRate * Time.deltaTime;
            }
            
            suspicionLevels[i] = Mathf.Clamp01(suspicionLevels[i] + suspicionChange);
        }
    }

    void EvaluateAggro()
    {
        float maxSuspicion = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (suspicionLevels[i] > maxSuspicion)
            {
                maxSuspicion = suspicionLevels[i];
            }
        }
        if (maxSuspicion >= SuspicionThreshold)
        {
            CurrentState = EnemyState.Chasing;
        }
        else if (CurrentState == EnemyState.Chasing && maxSuspicion < SuspicionThreshold * 0.5f)
        {
            CurrentState = EnemyState.Patrolling;
        }
    }

    Transform GetMostSuspiciousPlayer()
    {
        int index = -1;
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (suspicionLevels[i] > max)
            {
                max = suspicionLevels[i];
                index = i;
            }
        }
        if (index < 0 || index >= Players.Count) return null;
        return Players[index];
    }

    void EnsureSuspicionArray()
    {
        if (Players == null)
        {
            suspicionLevels = new float[0];
            return;
        }

        if (suspicionLevels == null || suspicionLevels.Length != Players.Count)
        {
            suspicionLevels = new float[Players.Count];
        }
    }

    float GetMaxSuspicion()
    {
        if (suspicionLevels == null || suspicionLevels.Length == 0) return 0f;
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (suspicionLevels[i] > max)
            {
                max = suspicionLevels[i];
            }
        }
        return max;
    }

    bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;
        
        float dist = Vector3.Distance(transform.position, player.position);
        
        // Must be within detection range
        if (dist > DetectionRange) return false;
        
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        
        // Must be within sight cone
        if (angle > SightAngle / 2f) return false;
        
        // Must have line of sight (no obstacles)
        if (Physics.Raycast(transform.position, dirToPlayer, dist, ObstacleMask))
        {
            return false;
        }
        
        return true;
    }

    float GetPlayerMask(Transform player)
    {

        var maskComponent = player.GetComponent<PlayerMask>();
        return maskComponent != null ? maskComponent.GetMaskEffect() : 1f;
    }

    Transform GetClosestPlayer()
    {
        Transform closest = null;
        float minDist = Mathf.Infinity;
        foreach (var player in Players)
        {
            if (player == null) continue;
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = player;
            }
        }
        return closest;
    }

    void OnDrawGizmos()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);

        // Teleport radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, teleportRadius);

        // Sight cone
        Vector3 left = Quaternion.Euler(0f, -SightAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, SightAngle * 0.5f, 0f) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + left * DetectionRange);
        Gizmos.DrawLine(transform.position, transform.position + right * DetectionRange);

        // Patrol points
        if (PatrolPoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < PatrolPoints.Count; i++)
            {
                if (PatrolPoints[i] == null) continue;
                Gizmos.DrawSphere(PatrolPoints[i].position, 0.3f);
            }
        }

        // Hunting radii around last known player position
        if (lastKnownPlayerPos != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(lastKnownPlayerPos, huntRadiusMin);
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(lastKnownPlayerPos, huntRadiusMax);
        }

        // Current destination
        if (Agent != null && Agent.hasPath)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, Agent.destination);
            Gizmos.DrawWireSphere(Agent.destination, 0.4f);
        }

        // Watch target line
        if (isWatchingPlayer && watchTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, watchTarget.position);
        }
    }

    void OnGUI()
    {
        if (!showSuspicionDebugUI) return;

        float maxSuspicion = GetMaxSuspicion();
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 260, 20), "[REMOVE LATER] Suspicion:" + maxSuspicion.ToString("0.00"));

        float barWidth = 200f;
        float barHeight = 12f;
        float x = 10f;
        float y = 30f;
        GUI.Box(new Rect(x, y, barWidth, barHeight), string.Empty);
        float filled = Mathf.Clamp01(maxSuspicion / Mathf.Max(0.01f, SuspicionThreshold));
        GUI.color = Color.red;
        GUI.Box(new Rect(x, y, barWidth * filled, barHeight), string.Empty);
        GUI.color = Color.white;
    }
}