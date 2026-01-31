using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;

public class GraveyardMinigame : MonoBehaviourPunCallbacks
{
    [Header("Minigame Settings")]
    [SerializeField] private List<Gravestone> gravestones = new List<Gravestone>();
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask gravestoneLayerMask;
    
    [Header("Reward Settings")]
    [SerializeField] private GameObject rewardPrefab;
    [SerializeField] private Transform rewardSpawnPoint;
    [SerializeField] private bool spawnRewardOnComplete = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip correctClickSound;
    [SerializeField] private AudioClip wrongClickSound;
    [SerializeField] private AudioClip completeSound;
    private AudioSource audioSource;

    private List<Gravestone> sortedGravestones = new List<Gravestone>();
    private int currentIndex = 0;
    private bool minigameActive = false;
    private bool minigameCompleted = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        InitializeMinigame();
    }

    private void InitializeMinigame()
    {
        Debug.Log($"[GraveyardMinigame] Initializing with {gravestones.Count} gravestones");
        
        foreach (var gravestone in gravestones)
        {
            gravestone.Initialize(this, gravestone.DeceasedName);
            Debug.Log($"[GraveyardMinigame] Added gravestone: {gravestone.DeceasedName}");
        }

        sortedGravestones = gravestones.OrderBy(g => g.DeceasedName).ToList();
        
        Debug.Log($"[GraveyardMinigame] Sorted order: {string.Join(", ", sortedGravestones.Select(g => g.DeceasedName))}");
        
        minigameActive = true;
        currentIndex = 0;
        minigameCompleted = false;
        
        Debug.Log($"[GraveyardMinigame] Minigame initialized! Active: {minigameActive}");
    }

    public void OnGravestoneClicked(Gravestone clickedGravestone)
    {
        Debug.Log($"[GraveyardMinigame] Gravestone clicked: {clickedGravestone.DeceasedName}");
        
        if (!minigameActive || minigameCompleted)
        {
            Debug.Log($"[GraveyardMinigame] Minigame not active or completed. Active: {minigameActive}, Completed: {minigameCompleted}");
            return;
        }

        string expectedName = sortedGravestones[currentIndex].DeceasedName;
        Debug.Log($"[GraveyardMinigame] Expected: {expectedName}, Got: {clickedGravestone.DeceasedName}");
        
        if (sortedGravestones[currentIndex] == clickedGravestone)
        {
            Debug.Log($"[GraveyardMinigame] CORRECT! Progress: {currentIndex + 1}/{sortedGravestones.Count}");
            clickedGravestone.SetGlowState(true);
            PlaySound(correctClickSound);
            
            currentIndex++;

            if (currentIndex >= sortedGravestones.Count)
            {
                CompleteMinigame();
            }
        }
        else
        {
            Debug.Log($"[GraveyardMinigame] WRONG! Resetting...");
            PlaySound(wrongClickSound);
            ResetMinigame();
        }
    }

    private void CompleteMinigame()
    {
        minigameCompleted = true;
        minigameActive = false;
        
        PlaySound(completeSound);
        
        foreach (var gravestone in gravestones)
        {
            gravestone.SetClickable(false);
        }

        if (spawnRewardOnComplete && rewardPrefab != null && rewardSpawnPoint != null)
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Instantiate(rewardPrefab.name, rewardSpawnPoint.position, rewardSpawnPoint.rotation);
            }
            else
            {
                Instantiate(rewardPrefab, rewardSpawnPoint.position, rewardSpawnPoint.rotation);
            }
        }

        Debug.Log("Minigame do cemitério completo! Recompensa desbloqueada!");
    }

    private void ResetMinigame()
    {
        currentIndex = 0;
        
        foreach (var gravestone in gravestones)
        {
            gravestone.ResetGravestone();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public bool IsMinigameActive()
    {
        return minigameActive && !minigameCompleted;
    }

    public bool IsMinigameCompleted()
    {
        return minigameCompleted;
    }

    public int GetCurrentProgress()
    {
        return currentIndex;
    }

    public int GetTotalGravestones()
    {
        return sortedGravestones.Count;
    }

    public string GetNextExpectedName()
    {
        if (currentIndex < sortedGravestones.Count)
        {
            return sortedGravestones[currentIndex].DeceasedName;
        }
        return "";
    }

    public Gravestone FindGravestoneInRange(Vector3 playerPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(playerPosition, interactRange, gravestoneLayerMask);
        
        Debug.Log($"[GraveyardMinigame] Found {colliders.Length} colliders in range");
        
        float closestDistance = float.MaxValue;
        Gravestone closestGravestone = null;

        foreach (Collider col in colliders)
        {
            Gravestone gravestone = col.GetComponent<Gravestone>();
            if (gravestone != null)
            {
                float distance = Vector3.Distance(playerPosition, col.transform.position);
                Debug.Log($"[GraveyardMinigame] Found gravestone {gravestone.DeceasedName} at distance {distance}");
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGravestone = gravestone;
                }
            }
        }

        if (closestGravestone != null)
        {
            Debug.Log($"[GraveyardMinigame] Closest gravestone: {closestGravestone.DeceasedName}");
        }
        else
        {
            Debug.Log($"[GraveyardMinigame] No gravestone found in range");
        }

        return closestGravestone;
    }
}
