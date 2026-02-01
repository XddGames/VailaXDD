using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class InventoryManager : MonoBehaviourPun
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory Settings")]
    [SerializeField] private int maxSlots = 4;
    
    [Header("Starting Items")]
    [SerializeField] private bool startWithFlashlight = false; // Changed to false - will be picked up
    [SerializeField] private Sprite flashlightIcon;
    [SerializeField] private GameObject flashlightPrefab; // Prefab to instantiate

    [Header("References")]
    [SerializeField] private FlashlightController flashlightController;
    
    private Transform holdPoint;
    private GameObject currentHeldItem;
    private List<InventoryItem> items = new List<InventoryItem>();
    private int selectedSlot = -1;
    private PlayerController playerController;

    public System.Action OnInventoryChanged;

    private void Awake()
    {
        // Find holdpoint in children - ALWAYS do this for all players (needed for network sync)
        holdPoint = FindHoldPoint();
        playerController = GetComponent<PlayerController>();
        
        // Only set instance for local player
        if (photonView != null && !photonView.IsMine) return;
        
        Instance = this;
    }

    private Transform FindHoldPoint()
    {
        // Search in children for holdpoint tag
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.CompareTag("holdpoint"))
            {
                return child;
            }
        }
        Debug.LogWarning("InventoryManager: No holdpoint found with tag 'holdpoint'");
        return null;
    }

    private void Start()
    {
        if (photonView != null && !photonView.IsMine) return;

        // Add starting items (if enabled)
        if (startWithFlashlight)
        {
            PickupFlashlight(null); // Spawn flashlight directly
        }

        // Trigger UI update
        OnInventoryChanged?.Invoke();
    }

    private void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        
        // Don't allow inventory interaction when dead/spectating
        if (!IsAlive()) return;

        HandleSlotInput();
    }
    
    private bool IsAlive()
    {
        if (playerController == null) return true; // Fallback if no PlayerController
        return playerController.GetCurrentState() == PlayerState.Alive;
    }

    private void HandleSlotInput()
    {
        // Number keys 1-4 for slots
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);

        // Scroll wheel to cycle slots
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0 && items.Count > 0)
        {
            int newSlot = selectedSlot + (scroll > 0 ? -1 : 1);
            if (newSlot < 0) newSlot = items.Count - 1;
            if (newSlot >= items.Count) newSlot = 0;
            SelectSlot(newSlot);
        }

        // Q to unequip current item
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UnequipAll();
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return;

        // If clicking the same slot, toggle equip
        if (selectedSlot == slotIndex && items[slotIndex].isEquipped)
        {
            UnequipItem(slotIndex);
            selectedSlot = -1;
        }
        else
        {
            // Unequip previous item
            if (selectedSlot >= 0 && selectedSlot < items.Count)
            {
                UnequipItem(selectedSlot);
            }

            selectedSlot = slotIndex;
            EquipItem(slotIndex);
        }

        OnInventoryChanged?.Invoke();
    }

    private void EquipItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return;

        InventoryItem item = items[slotIndex];
        item.isEquipped = true;

        switch (item.itemType)
        {
            case InventoryItem.ItemType.Flashlight:
                // Show the held flashlight
                if (currentHeldItem != null)
                {
                    currentHeldItem.SetActive(true);
                }
                if (flashlightController != null)
                {
                    flashlightController.SetEquipped(true);
                }
                // Sync to other players
                if (photonView != null)
                {
                    bool lightOn = flashlightController != null && flashlightController.IsLightOn();
                    photonView.RPC("RPC_SyncFlashlightEquipped", RpcTarget.Others, true, lightOn);
                }
                break;
        }
    }

    private void UnequipItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return;

        InventoryItem item = items[slotIndex];
        item.isEquipped = false;

        switch (item.itemType)
        {
            case InventoryItem.ItemType.Flashlight:
                // Hide the held flashlight
                if (currentHeldItem != null)
                {
                    currentHeldItem.SetActive(false);
                }
                if (flashlightController != null)
                {
                    flashlightController.SetEquipped(false);
                }
                // Sync to other players
                if (photonView != null)
                {
                    photonView.RPC("RPC_SyncFlashlightEquipped", RpcTarget.Others, false, false);
                }
                break;
        }
    }

    public void UnequipAll()
    {
        for (int i = 0; i < items.Count; i++)
        {
            UnequipItem(i);
        }
        selectedSlot = -1;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Called when player dies - destroys held items and clears inventory
    /// </summary>
    public void OnPlayerDeath()
    {
        // Destroy the held flashlight
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }
        
        flashlightController = null;
        
        // Clear the inventory
        items.Clear();
        selectedSlot = -1;
        
        // Sync to other players
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("RPC_OnPlayerDeath", RpcTarget.Others);
        }
        
        OnInventoryChanged?.Invoke();
        
        Debug.Log("[InventoryManager] Player died - inventory cleared");
    }
    
    [PunRPC]
    void RPC_OnPlayerDeath()
    {
        // Remote player died - destroy their held item on our client
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }
        flashlightController = null;
        items.Clear();
        selectedSlot = -1;
    }

    public bool AddItem(InventoryItem item)
    {
        if (items.Count >= maxSlots)
        {
            Debug.Log("Inventory full!");
            return false;
        }

        items.Add(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return false;

        if (items[slotIndex].isEquipped)
        {
            UnequipItem(slotIndex);
        }

        items.RemoveAt(slotIndex);
        
        if (selectedSlot >= items.Count)
        {
            selectedSlot = items.Count - 1;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Call this when player picks up a flashlight in the world.
    /// Pass the world object to destroy it, or null to just spawn one.
    /// </summary>
    public void PickupFlashlight(GameObject worldFlashlight)
    {
        if (photonView != null && !photonView.IsMine) return;
        
        Debug.Log("[InventoryManager] PickupFlashlight called");
        
        // Handle the world pickup object across the network
        if (worldFlashlight != null)
        {
            Vector3 pickupPos = worldFlashlight.transform.position;
            
            // Disable locally immediately
            worldFlashlight.SetActive(false);
            
            // Tell all other clients to disable/destroy this pickup by position
            if (photonView != null)
            {
                photonView.RPC("RPC_DestroyPickupByPosition", RpcTarget.Others, pickupPos);
            }
            
            // Actually destroy it locally
            Destroy(worldFlashlight);
        }

        // Add to inventory
        AddItem(new InventoryItem("Flashlight", flashlightIcon, InventoryItem.ItemType.Flashlight));

        // Spawn flashlight at holdpoint locally
        SpawnFlashlightAtHoldpoint();
        
        // Tell other clients to spawn a flashlight on this player
        if (photonView != null)
        {
            photonView.RPC("RPC_SpawnFlashlightOnPlayer", RpcTarget.Others);
        }

        Debug.Log("Picked up flashlight!");
    }
    
    private void SpawnFlashlightAtHoldpoint()
    {
        if (flashlightPrefab != null && holdPoint != null)
        {
            // Destroy old held item if any
            if (currentHeldItem != null)
            {
                Destroy(currentHeldItem);
            }

            currentHeldItem = Instantiate(flashlightPrefab, holdPoint.position, holdPoint.rotation, holdPoint);
            
            // Preserve the prefab's original scale (don't inherit parent scale)
            currentHeldItem.transform.localScale = flashlightPrefab.transform.localScale;
            
            // Get the FlashlightController from the instantiated object
            flashlightController = currentHeldItem.GetComponent<FlashlightController>();
            
            // Tell the flashlight who owns it (so only local player can control it)
            if (flashlightController != null)
            {
                flashlightController.SetOwner(photonView);
            }
            
            // Start hidden until player equips it (presses 1)
            currentHeldItem.SetActive(false);
        }
    }
    
    private IEnumerator DestroyPickupDelayed(GameObject pickup, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pickup != null)
        {
            PhotonView pv = pickup.GetComponent<PhotonView>();
            if (pv != null && PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(pickup);
            }
            else
            {
                Destroy(pickup);
            }
        }
    }

    /// <summary>
    /// Check if player has a specific item type
    /// </summary>
    public bool HasItem(InventoryItem.ItemType itemType)
    {
        foreach (var item in items)
        {
            if (item.itemType == itemType) return true;
        }
        return false;
    }

    // Network sync RPCs for flashlight
    [PunRPC]
    void RPC_SyncFlashlightState(bool lightOn)
    {
        Debug.Log($"[RPC] RPC_SyncFlashlightState received: lightOn={lightOn}, flashlightController={(flashlightController != null ? "exists" : "NULL")}");
        if (flashlightController != null)
        {
            flashlightController.SyncFromNetwork(true, lightOn);
        }
    }
    
    [PunRPC]
    void RPC_SpawnFlashlightOnPlayer()
    {
        Debug.Log($"[RPC] RPC_SpawnFlashlightOnPlayer received, holdPoint={(holdPoint != null ? "exists" : "NULL")}");
        // Another player picked up a flashlight, spawn it on them
        SpawnFlashlightAtHoldpoint();
        Debug.Log($"Spawned flashlight on remote player {photonView.Owner.NickName}");
    }
    
    [PunRPC]
    void RPC_SyncFlashlightEquipped(bool equipped, bool lightOn)
    {
        Debug.Log($"[RPC] RPC_SyncFlashlightEquipped received: equipped={equipped}, lightOn={lightOn}");
        if (currentHeldItem != null)
        {
            currentHeldItem.SetActive(equipped);
        }
        if (flashlightController != null)
        {
            flashlightController.SyncFromNetwork(equipped, lightOn);
        }
    }
    
    [PunRPC]
    void RPC_RequestDestroyPickup(int viewID)
    {
        // Master client destroys the pickup
        PhotonView pv = PhotonView.Find(viewID);
        if (pv != null && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(pv.gameObject);
        }
    }
    
    [PunRPC]
    void RPC_DestroyPickupByPosition(Vector3 position)
    {
        // Find and destroy pickup near this position
        PickupFlashlight[] pickups = FindObjectsOfType<PickupFlashlight>();
        foreach (var pickup in pickups)
        {
            if (Vector3.Distance(pickup.transform.position, position) < 0.5f)
            {
                Destroy(pickup.gameObject);
                break;
            }
        }
    }

    public List<InventoryItem> GetItems() => items;
    public int GetSelectedSlot() => selectedSlot;
    public int GetMaxSlots() => maxSlots;
}
