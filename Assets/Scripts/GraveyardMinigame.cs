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
            Debug.LogError("[GRAVE] ERRO: Lista de gravestones está vazia! Adiciona as lápides no Inspector do GraveyardMinigame.");
            return;
        }

        foreach (var gravestone in gravestones)
        {
            if (gravestone == null)
            {
                Debug.LogWarning("[GRAVE] Uma gravestone na lista é null, ignorando...");
                continue;
            }
            gravestone.Initialize(this, gravestone.DeceasedName);
        }

        sortedGravestones = gravestones.OrderBy(g => g.DeceasedName).ToList();
        
        minigameActive = true;
        currentIndex = 0;
        minigameCompleted = false;
        
        Debug.Log($"[GRAVE] Minigame inicializado com {sortedGravestones.Count} lápides");
    }

    public void OnGravestoneClicked(Gravestone clickedGravestone)
    {
        Debug.Log($"[GRAVE] OnGravestoneClicked: {clickedGravestone.DeceasedName}");
        Debug.Log($"[GRAVE] minigameActive: {minigameActive}, minigameCompleted: {minigameCompleted}");
        
        if (!minigameActive || minigameCompleted)
        {
            Debug.Log($"[GRAVE] Minigame não está ativo ou já foi completado!");
            return;
        }

        string expectedName = sortedGravestones[currentIndex].DeceasedName;
        Debug.Log($"[GRAVE] Esperado: {expectedName}, Clicado: {clickedGravestone.DeceasedName}, Index: {currentIndex}");
        
        if (sortedGravestones[currentIndex] == clickedGravestone)
        {
            Debug.Log($"[GRAVE] CORRETO! Acendendo glow amarelo");
            clickedGravestone.SetGlowState(true);
            PlaySound(correctClickSound);
            
            currentIndex++;
            Debug.Log($"[GRAVE] Progresso: {currentIndex}/{sortedGravestones.Count}");

            if (currentIndex >= sortedGravestones.Count)
            {
                Debug.Log($"[GRAVE] MINIGAME COMPLETO!");
                CompleteMinigame();
            }
        }
        else
        {
            Debug.Log($"[GRAVE] ERRADO! Resetando minigame");
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
        
        Debug.Log($"[GRAVE] OverlapSphere: {colliders.Length} colliders encontrados");
        Debug.Log($"[GRAVE] InteractRange: {interactRange}, LayerMask value: {gravestoneLayerMask.value}");
        
        float closestDistance = float.MaxValue;
        Gravestone closestGravestone = null;

        foreach (Collider col in colliders)
        {
            Debug.Log($"[GRAVE] Collider encontrado: {col.name} na layer {LayerMask.LayerToName(col.gameObject.layer)}");
            Gravestone gravestone = col.GetComponent<Gravestone>();
            if (gravestone != null)
            {
                float distance = Vector3.Distance(playerPosition, col.transform.position);
                Debug.Log($"[GRAVE] Gravestone válida: {gravestone.DeceasedName} a {distance}m");
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGravestone = gravestone;
                }
            }
            else
            {
                Debug.Log($"[GRAVE] Collider {col.name} NÃO tem componente Gravestone!");
            }
        }

        if (closestGravestone != null)
        {
            Debug.Log($"[GRAVE] Gravestone mais próxima: {closestGravestone.DeceasedName}");
        }
        else
        {
            Debug.Log($"[GRAVE] Nenhuma gravestone encontrada no range!");
        }

        return closestGravestone;
    }
}
