using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class GameNetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab; // Assign your player prefab in inspector

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints; // Assign spawn points in inspector
    [SerializeField] private float spawnRadius = 5f; // Random spawn radius if no spawn points set

    [Header("Auto-Find Spawn Points")]
    [SerializeField] private bool autoFindSpawnPoints = true;
    [SerializeField] private string spawnPointTag = "SpawnPoint"; // Tag for spawn point GameObjects
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(654.6893f, 13.3f, 661.1091f); // Default spawn if no spawn points


    private GameObject localPlayerInstance;

    private void Start()
    {
        if (autoFindSpawnPoints && (spawnPoints == null || spawnPoints.Length == 0))
        {
            GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag(spawnPointTag);
            spawnPoints = new Transform[spawnPointObjects.Length];
            for (int i = 0; i < spawnPointObjects.Length; i++)
            {
                spawnPoints[i] = spawnPointObjects[i].transform;
            }
            
            if (spawnPoints.Length > 0)
            {
                Debug.Log($"Auto-found {spawnPoints.Length} spawn points.");
            }
        }
        if (PhotonNetwork.IsMasterClient)
        {
            // Make sure you have an enemy prefab in Resources folder
            Vector3 enemySpawnPos = defaultSpawnPosition;
            if (enemySpawnPos == Vector3.zero)
            {
                enemySpawnPos = new Vector3(0, 1, 0); // Default position
            }
            
            GameObject enemy = PhotonNetwork.InstantiateRoomObject(
                "Enemy", // Your actual enemy prefab name (must be in Resources folder)
                enemySpawnPos, 
                Quaternion.identity
            );
            
            Debug.Log($"Master Client spawned enemy at {enemySpawnPos}");
        }
        // Spawn player immediately if connected
        if (PhotonNetwork.IsConnectedAndReady && localPlayerInstance == null)
        {
            SpawnPlayer();
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}. Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // Spawn player when entering the game scene
        if (localPlayerInstance == null)
        {
            SpawnPlayer();
        }
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab not assigned in GameNetworkManager!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion spawnRotation = Quaternion.identity;

        Debug.Log($"Spawning player at {spawnPosition}");

        localPlayerInstance = PhotonNetwork.Instantiate(
            playerPrefab.name,
            spawnPosition,
            spawnRotation
        );

        Debug.Log($"Player spawned! ViewID: {localPlayerInstance.GetComponent<PhotonView>().ViewID}");
    }

    private Vector3 GetSpawnPosition()
    {
        // If spawn points are defined, use them
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Use player's actor number to determine spawn point (consistent spawning)
            int spawnIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
            return spawnPoints[spawnIndex].position;
        }
        else
        {
            // Fallback: Use default spawn position
            return defaultSpawnPosition;
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        if (localPlayerInstance != null)
        {
            Destroy(localPlayerInstance);
            localPlayerInstance = null;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        Debug.Log($"Player {newPlayer.NickName} entered the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.Log($"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    // Optional: Call this if you want to wait for all players before allowing gameplay
    public bool AreAllPlayersReady()
    {
        return PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers;
    }
}