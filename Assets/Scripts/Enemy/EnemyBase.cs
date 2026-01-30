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

    [Header("Detection")]
    [SerializeField] private List<Transform> Players;
    [SerializeField] private float DetectionRange = 15f;
    [SerializeField] private float SightAngle = 90f;
    [SerializeField] private float SuspicionThreshold = 1.0f;
    [SerializeField] private LayerMask ObstacleMask;

    private NavMeshAgent Agent;
    private float[] suspicionLevels;

    

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
        suspicionLevels = new float[Players.Count];
    }

    void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        //belos eu confio

        switch (CurrentState)
        {
            case EnemyState.Idle:
                break;
            case EnemyState.Patrolling:
                //Patrol();
                break;
            case EnemyState.Chasing:
                //Chase();
                break;
        }
        UpdateSuspicion();
        CheckAggro();
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

    //this is abhorrent but its only 2 players so we ball
    void UpdateSuspicion()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Transform player = Players[i];
            if (player == null) continue;

            float suspicionGain = 0f;
            if (CanSeePlayer(player))
            {
                //calculate how much suspicion wil lbe gained based on position and if the mask is on or not
                float distance = Vector3.Distance(transform.position, player.position);
                float maskEffect = GetPlayerMask(player);
                suspicionGain = (1f - Mathf.Clamp01(distance / DetectionRange)) * (1f - maskEffect) * Time.deltaTime;
            }
            else
            {
                suspicionGain = -0.5f * Time.deltaTime;
            }
            suspicionLevels[i] = Mathf.Clamp01(suspicionLevels[i] + suspicionGain);
        }
    }

    void CheckAggro()
    {
        int suspiciousIndex = -1;
        float maxSuspicion = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (suspicionLevels[i] > maxSuspicion)
            {
                maxSuspicion = suspicionLevels[i];
                suspiciousIndex = i;
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

    Transform RevealedPlayer()
    {
        int index = 0;
        float max = 0f;
        for (int i = 0; i < suspicionLevels.Length; i++)
        {
            if (suspicionLevels[i] > max)
            {
                max = suspicionLevels[i];
                index = i;
            }
        }
        return Players[index];
    }

    bool CanSeePlayer(Transform player)
    {
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle < SightAngle / 2f)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (!Physics.Raycast(transform.position, dirToPlayer, dist, ObstacleMask))
            {
                return true;
            }
        }
        return false;
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
}