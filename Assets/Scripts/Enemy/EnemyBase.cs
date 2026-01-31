using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Photon.Pun;

public class EnemyBase : MonoBehaviourPunCallbacks
{
    public enum EnemyState { Teleporting, Observing, Patrolling, Chasing }

    [Header("Players")]
    [SerializeField] private List<Transform> Players;

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

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private EnemyState state = EnemyState.Teleporting;
    private NavMeshAgent agent;
    private float[] suspicionLevels;
    private float teleportTimer;
    private Transform target;
    private List<Vector3> patrolPoints = new List<Vector3>();
    private int patrolIndex;


    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on " + gameObject.name);
        }
        else
        {
            Debug.Log("NavMeshAgent found and initialized");
        }
        
        suspicionLevels = new float[Players?.Count ?? 0];
        
        // Configure agent to prevent sliding
        if (agent != null)
        {
            agent.enabled = true;
            agent.angularSpeed = 200f; // Faster rotation
            agent.acceleration = 12f; // Faster acceleration/deceleration
            agent.stoppingDistance = 0.5f; // Stop close to destination
            agent.autoBraking = true; // Auto brake when approaching destination
            agent.updateRotation = true; // Let agent handle rotation
            agent.updatePosition = true; // Let agent handle position
            agent.updateUpAxis = true; // Follow terrain properly
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            
            Debug.Log("NavMeshAgent configured - Speed limit: " + agent.speed);
        }
        
        // If there's a Rigidbody, configure it
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // NavMeshAgent should control movement
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Debug.Log("Rigidbody set to kinematic");
        }
    }

    void Update()
    {
        if (Players == null || Players.Count == 0) return;
        if (suspicionLevels.Length != Players.Count) suspicionLevels = new float[Players.Count];

        DecaySuspicion();

        switch (state)
        {
            case EnemyState.Teleporting: UpdateTeleporting(); break;
            case EnemyState.Observing: UpdateObserving(); break;
            case EnemyState.Patrolling: UpdatePatrolling(); break;
            case EnemyState.Chasing: UpdateChasing(); break;
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
        {
            Debug.LogError("NavMeshAgent is null!");
            return;
        }
        
        agent.isStopped = true;

        Transform nearest = GetNearestInRange(observeRange);
        if (nearest != null)
        {
            Debug.Log($"Player detected at {Vector3.Distance(transform.position, nearest.position):F1}m - switching to Observing");
            state = EnemyState.Observing;
            target = nearest;
            return;
        }

        teleportTimer += Time.deltaTime;
        if (teleportTimer >= teleportInterval)
        {
            Debug.Log($"Teleport timer reached {teleportTimer:F1}s - attempting teleport");
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
        Debug.Log("=== TELEPORT CALLED ===");
        
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
            Debug.LogWarning("No players found for teleporting!");
            return;
        }
        
        // Find a valid player
        Transform randomPlayer = null;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i] != null)
            {
                randomPlayer = Players[i];
                Debug.Log($"Found valid player at index {i}: {randomPlayer.name}");
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
        
        Debug.Log($"Teleport target: {targetPoint} (distance to player: {randomDistance:F1}m, angle: {randomAngle:F0}°)");
        
        // Try multiple times with different search parameters
        bool teleported = false;
        int navMaskAll = -1; // Use -1 instead of NavMesh.AllAreas for better compatibility
        
        for (int attempt = 0; attempt < 5 && !teleported; attempt++)
        {
            // Use the calculated target point
            Vector3 pos = targetPoint;
            
            // Sample from player's height instead of adding 50
            pos.y = randomPlayer.position.y + 10f;
            
            Debug.Log($"Attempt {attempt + 1}: Searching for NavMesh near: {pos}");

            // Try progressively larger search radii
            float[] searchRadii = { 50f, 100f, 200f };
            foreach (float searchRadius in searchRadii)
            {
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, searchRadius, navMaskAll))
                {
                    Debug.Log($"<color=green>NavMesh FOUND at: {hit.position} (search radius: {searchRadius}m)</color>");
                    
                    // Add bigger height offset to prevent spawning inside floor
                    Vector3 warpPosition = hit.position;
                    warpPosition.y += 2f; // Lift 2 meters above NavMesh surface (increased from 1m)
                    
                    if (agent.Warp(warpPosition))
                    {
                        // Force re-enable agent after warp
                        agent.enabled = true;
                        agent.isStopped = false;
                        
                        Debug.Log($"<color=green>WARP SUCCEEDED! New position: {transform.position}, On NavMesh: {agent.isOnNavMesh}</color>");
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
                    Debug.Log($"No NavMesh found with {searchRadius}m search radius");
                }
            }
        }
        
        if (!teleported)
        {
            Debug.LogError($"<color=red>TELEPORT COMPLETELY FAILED after 5 attempts! NavMesh might not be baked on terrain. Enemy staying at: {transform.position}</color>");
        }
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

        int idx = Players.IndexOf(target);
        if (idx >= 0)
        {
            // Apply mask effect - mask reduces suspicion gain
            float maskEffect = GetPlayerMask(target);
            
            // Distance-based suspicion gain - slower from far away, faster up close
            float distance = Vector3.Distance(transform.position, target.position);
            float distanceRatio = distance / observeRange; // 0 = very close, 1 = at max range
            float distanceBasedGain = Mathf.Lerp(observeGainRateClose, observeGainRateFar, distanceRatio);
            
            float effectiveSuspicionGain = distanceBasedGain * maskEffect * Time.deltaTime;
            
            suspicionLevels[idx] += effectiveSuspicionGain;
            suspicionLevels[idx] = Mathf.Clamp01(suspicionLevels[idx]);

            if (suspicionLevels[idx] >= suspicionToPatrol)
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
            Debug.Log("No suspicious player found, returning to teleporting");
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
        
        // Gain suspicion while patrolling if player is nearby
        float distToPlayer = Vector3.Distance(transform.position, mostSus.position);
        if (distToPlayer <= patrolSuspicionRange)
        {
            int playerIdx = Players.IndexOf(mostSus);
            if (playerIdx >= 0)
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

        if (patrolPoints.Count == 0)
        {
            Debug.Log("Generating patrol points");
            GeneratePatrol(mostSus.position);
        }

        if (patrolPoints.Count > 0)
        {
            agent.SetDestination(patrolPoints[patrolIndex]);
            
            if (Time.frameCount % 60 == 0) // Log every 60 frames
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

    void GeneratePatrol(Vector3 center)
    {
        patrolPoints.Clear();
        Debug.Log($"Generating patrol points around {center}");
        
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
                Debug.Log($"Patrol point {i} added at {hit.position}");
            }
            else
            {
                Debug.LogWarning($"Failed to find NavMesh for patrol point {i} near {pos}");
            }
        }
        
        patrolIndex = 0;
        Debug.Log($"Generated {patrolPoints.Count} patrol points");
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

        agent.SetDestination(mostSus.position);

        if (GetMaxSuspicion() < suspicionToChase * 0.7f)
        {
            state = EnemyState.Patrolling;
            target = mostSus;
            GeneratePatrol(mostSus.position);
        }
    }

    // ===== HELPERS =====
    void DecaySuspicion()
    {
        // Don't decay suspicion when actively observing, patrolling, or chasing
        if (state == EnemyState.Observing || state == EnemyState.Patrolling || state == EnemyState.Chasing)
        {
            // Only decay if player is out of range or wearing effective mask
            for (int i = 0; i < suspicionLevels.Length; i++)
            {
                if (Players[i] == null) continue;
                
                float distance = Vector3.Distance(transform.position, Players[i].position);
                bool playerInRange = distance <= observeRange;
                
                // Only decay if player is far away
                if (!playerInRange)
                {
                    suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * Time.deltaTime);
                }
            }
        }
        else if (state == EnemyState.Teleporting)
        {
            // Normal decay when teleporting (not engaged)
            for (int i = 0; i < suspicionLevels.Length; i++)
                suspicionLevels[i] = Mathf.Max(0f, suspicionLevels[i] - decayRate * Time.deltaTime);
        }
    }

    Transform GetNearestInRange(float range)
    {
        Transform nearest = null;
        float minDist = range;
        foreach (Transform p in Players)
        {
            if (p == null) continue;
            float d = Vector3.Distance(transform.position, p.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = p;
            }
        }
        return nearest;
    }

    Transform GetMostSuspicious()
    {
        int idx = -1;
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
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
        foreach (float s in suspicionLevels)
            if (s > max) max = s;
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
        
        PlayerMask maskComponent = player.GetComponent<PlayerMask>();
        if (maskComponent != null)
        {
            float maskEffect = maskComponent.GetMaskEffect();
            // maskEffect is 0 when mask is on (no suspicion gain), 1 when off (full gain)
            return maskEffect;
        }
        return 1f; // No mask component = full suspicion gain
    }

    // ===== DEBUG =====
    void OnDrawGizmos()
    {
        if (!showDebug) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, observeRange);

        if (target != null)
        {
            Gizmos.color = Color.red;
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