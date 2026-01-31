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
        if (gravestones == null || gravestones.Count == 0)
        {
            return;
        }

        foreach (var gravestone in gravestones)
        {
            if (gravestone == null)
            {
                continue;
            }
            gravestone.Initialize(this, gravestone.DeceasedName);
        }

        sortedGravestones = gravestones.OrderBy(g => g.DeceasedName).ToList();
        
        minigameActive = true;
        currentIndex = 0;
        minigameCompleted = false; 
    }

    public void OnGravestoneClicked(Gravestone clickedGravestone)
    {
        
        if (!minigameActive || minigameCompleted)
        {
            return;
        }

        string expectedName = sortedGravestones[currentIndex].DeceasedName;
        
        if (sortedGravestones[currentIndex] == clickedGravestone)
        {
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
        
        float closestDistance = float.MaxValue;
        Gravestone closestGravestone = null;

        foreach (Collider col in colliders)
        {
            Gravestone gravestone = col.GetComponent<Gravestone>();
            if (gravestone != null)
            {
                float distance = Vector3.Distance(playerPosition, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGravestone = gravestone;
                }
            }
            else
            {
            }
        }

        return closestGravestone;
    }
}
