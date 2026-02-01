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
    [SerializeField] private bool useRandomNames = true;
    
    [Header("Reward Settings")]
    [SerializeField] private GameObject rewardPrefab;
    [SerializeField] private Transform rewardSpawnPoint;
    [SerializeField] private bool spawnRewardOnComplete = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip correctClickSound;
    [SerializeField] private AudioClip wrongClickSound;
    [SerializeField] private AudioClip completeSound;
    private AudioSource audioSource;

    [System.Serializable]
    public class GravestoneData
    {
        public string name;
        public int yearOfDeath;

        public GravestoneData(string n, int y)
        {
            name = n;
            yearOfDeath = y;
        }

        public string GetFullText()
        {
            return $"{name}\n{yearOfDeath}";
        }
    }

    private List<GravestoneData> namePool = new List<GravestoneData>
    {
        new GravestoneData("Albert Morrison", 1847),
        new GravestoneData("Benjamin Clarke", 1852),
        new GravestoneData("Catherine Wells", 1859),
        new GravestoneData("Daniel Foster", 1863),
        new GravestoneData("Eleanor Price", 1871),
        new GravestoneData("Francis Harper", 1878),
        new GravestoneData("Grace Mitchell", 1884),
        new GravestoneData("Harold Stevens", 1891),
        new GravestoneData("Isabella Grant", 1898),
        new GravestoneData("James Patterson", 1905),
        new GravestoneData("Katherine Ross", 1912),
        new GravestoneData("Leonard Wright", 1919),
        new GravestoneData("Margaret Cole", 1923),
        new GravestoneData("Nicholas Ward", 1931),
        new GravestoneData("Olivia Reed", 1938),
        new GravestoneData("Patrick Hughes", 1945),
        new GravestoneData("Rebecca Stone", 1952),
        new GravestoneData("Samuel Brooks", 1967),
        new GravestoneData("Victoria Black", 1974),
        new GravestoneData("William Turner", 1982)
    };

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

        // Assign random names if enabled
        if (useRandomNames)
        {
            AssignRandomNames();
        }

        foreach (var gravestone in gravestones)
        {
            if (gravestone == null)
            {
                continue;
            }
            // Initialize with the name that was set (either manual or random)
            if (!string.IsNullOrEmpty(gravestone.DeceasedName))
            {
                gravestone.Initialize(this, gravestone.DeceasedName);
            }
        }

        sortedGravestones = gravestones.OrderBy(g => g.DeceasedName).ToList();
        
        minigameActive = true;
        currentIndex = 0;
        minigameCompleted = false; 
    }

    private void AssignRandomNames()
    {
        // Create a shuffled copy of the name pool
        List<GravestoneData> availableNames = new List<GravestoneData>(namePool);
        
        // Shuffle using Fisher-Yates algorithm
        for (int i = availableNames.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            GravestoneData temp = availableNames[i];
            availableNames[i] = availableNames[randomIndex];
            availableNames[randomIndex] = temp;
        }

        // Assign names to gravestones
        for (int i = 0; i < gravestones.Count && i < availableNames.Count; i++)
        {
            if (gravestones[i] != null)
            {
                gravestones[i].SetName(availableNames[i].name);
                gravestones[i].SetDisplayText(availableNames[i].GetFullText());
            }
        }
    }

    public void OnGravestoneClicked(Gravestone clickedGravestone)
    {
        if (!minigameActive || minigameCompleted)
        {
            return;
        }

        // Find the index of the clicked gravestone in the sorted list
        int clickedIndex = sortedGravestones.IndexOf(clickedGravestone);
        
        // Check if this is the correct gravestone in the sequence
        if (clickedIndex == currentIndex)
        {
            // Synchronize the correct click across all clients
            if (PhotonNetwork.IsConnected && photonView != null)
            {
                photonView.RPC(nameof(RPC_CorrectGravestoneClicked), RpcTarget.AllBuffered, clickedIndex);
            }
            else
            {
                ProcessCorrectClick(clickedIndex);
            }
        }
        else
        {
            // Synchronize the wrong click across all clients
            if (PhotonNetwork.IsConnected && photonView != null)
            {
                photonView.RPC(nameof(RPC_WrongGravestoneClicked), RpcTarget.AllBuffered);
            }
            else
            {
                ProcessWrongClick();
            }
        }
    }

    [PunRPC]
    private void RPC_CorrectGravestoneClicked(int gravestoneIndex)
    {
        ProcessCorrectClick(gravestoneIndex);
    }

    [PunRPC]
    private void RPC_WrongGravestoneClicked()
    {
        ProcessWrongClick();
    }

    private void ProcessCorrectClick(int gravestoneIndex)
    {
        if (gravestoneIndex >= 0 && gravestoneIndex < sortedGravestones.Count)
        {
            sortedGravestones[gravestoneIndex].SetGlowState(true);
            PlaySound(correctClickSound);
            
            currentIndex++;

            if (currentIndex >= sortedGravestones.Count)
            {
                CompleteMinigame();
            }
        }
    }

    private void ProcessWrongClick()
    {
        PlaySound(wrongClickSound);
        ResetMinigame();
    }

    private void CompleteMinigame()
    {
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            // Only master client spawns the reward to avoid duplicates
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC(nameof(RPC_CompleteMinigame), RpcTarget.AllBuffered);
            }
        }
        else
        {
            FinalizeCompletion();
        }
    }

    [PunRPC]
    private void RPC_CompleteMinigame()
    {
        FinalizeCompletion();
    }

    private void FinalizeCompletion()
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
            if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Instantiate(rewardPrefab.name, rewardSpawnPoint.position, rewardSpawnPoint.rotation);
            }
            else if (!PhotonNetwork.IsConnected)
            {
                Instantiate(rewardPrefab, rewardSpawnPoint.position, rewardSpawnPoint.rotation);
            }
        }
    }

    private void ResetMinigame()
    {
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC(nameof(RPC_ResetMinigame), RpcTarget.AllBuffered);
            }
        }
        else
        {
            PerformReset();
        }
    }

    [PunRPC]
    private void RPC_ResetMinigame()
    {
        PerformReset();
    }

    private void PerformReset()
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
        // Debug raycast to understand what's being hit
        Camera cam = Camera.main;
        Vector3 screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        GameObject cross = GameObject.FindWithTag("Crosshair") ?? GameObject.Find("Crosshair");
        if (cross != null)
        {
            RectTransform rt = cross.GetComponent<RectTransform>();
            if (rt != null)
            {
                screenPoint = rt.position;
            }
        }

        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPoint);

            // Show ALL hits, not just gravestones
            RaycastHit[] allHits = Physics.RaycastAll(ray, interactRange);

            // Now filter by layer mask
            RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, gravestoneLayerMask);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    Gravestone g = h.collider.GetComponent<Gravestone>();
                    if (g != null)
                    {
                        return g;
                    }
                }
            }
        }

        // Fallback: nearest gravestone to the player position
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
        }

        return closestGravestone;
    }
}
