using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class InventoryUI : MonoBehaviour
{
    [Header("Slot References")]
    [SerializeField] private Image[] slotImages;       // The slot background images
    [SerializeField] private Image[] slotIconImages;   // The icon images inside slots
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalSlotColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color selectedSlotColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color equippedSlotColor = Color.green;
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color filledIconColor = Color.white;

    private InventoryManager inventoryManager;
    private bool searchingForInventory = false;

    private void Start()
    {
        TryFindInventoryManager();
        
        if (inventoryManager == null)
        {
            searchingForInventory = true;
            Debug.Log("InventoryUI: Searching for InventoryManager...");
        }

        // Initialize all slots as empty
        ClearAllSlots();
    }

    private void TryFindInventoryManager()
    {
        if (inventoryManager != null) return;

        InventoryManager[] allManagers = FindObjectsOfType<InventoryManager>();
        
        // First try to find local player's inventory
        foreach (InventoryManager manager in allManagers)
        {
            PhotonView pv = manager.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                ConnectToInventory(manager);
                return;
            }
        }

        // Fallback: Single player mode or no PhotonView
        if (allManagers.Length >= 1)
        {
            // Try to find one without checking IsMine
            foreach (InventoryManager manager in allManagers)
            {
                PhotonView pv = manager.GetComponent<PhotonView>();
                // If no PhotonView or we're not in a room, just use first one
                if (pv == null || !PhotonNetwork.IsConnected)
                {
                    ConnectToInventory(manager);
                    return;
                }
            }
        }
    }

    private void ConnectToInventory(InventoryManager manager)
    {
        inventoryManager = manager;
        inventoryManager.OnInventoryChanged += UpdateUI;
        searchingForInventory = false;
        Debug.Log("InventoryUI: Connected to InventoryManager!");
        UpdateUI();
    }

    private void Update()
    {
        if (searchingForInventory)
        {
            TryFindInventoryManager();
        }
    }

    private void OnDestroy()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryChanged -= UpdateUI;
        }
    }

    public void UpdateUI()
    {
        if (inventoryManager == null) 
        {
            Debug.LogWarning("InventoryUI: No inventory manager!");
            return;
        }

        var items = inventoryManager.GetItems();
        int selectedSlot = inventoryManager.GetSelectedSlot();

        Debug.Log($"InventoryUI: Updating UI - {items.Count} items, selected slot: {selectedSlot}");

        for (int i = 0; i < slotImages.Length; i++)
        {
            if (i < items.Count)
            {
                // Slot has an item
                InventoryItem item = items[i];
                
                Debug.Log($"InventoryUI: Slot {i} has {item.itemName}, icon: {(item.icon != null ? item.icon.name : "NULL")}");
                
                // Set icon
                if (slotIconImages[i] != null)
                {
                    slotIconImages[i].sprite = item.icon;
                    slotIconImages[i].color = item.icon != null ? filledIconColor : emptyIconColor;
                    slotIconImages[i].enabled = true;
                }

                // Set slot highlight
                if (slotImages[i] != null)
                {
                    if (item.isEquipped)
                    {
                        slotImages[i].color = equippedSlotColor;
                    }
                    else if (i == selectedSlot)
                    {
                        slotImages[i].color = selectedSlotColor;
                    }
                    else
                    {
                        slotImages[i].color = normalSlotColor;
                    }
                }
            }
            else
            {
                // Empty slot
                if (slotIconImages[i] != null)
                {
                    slotIconImages[i].sprite = null;
                    slotIconImages[i].color = emptyIconColor;
                }
                if (slotImages[i] != null)
                {
                    slotImages[i].color = normalSlotColor;
                }
            }
        }
    }

    private void ClearAllSlots()
    {
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] != null)
            {
                slotImages[i].color = normalSlotColor;
            }
            if (slotIconImages[i] != null)
            {
                slotIconImages[i].sprite = null;
                slotIconImages[i].color = emptyIconColor;
            }
        }
    }

    // Called from UI buttons if you want clickable slots
    public void OnSlotClicked(int slotIndex)
    {
        if (inventoryManager != null)
        {
            inventoryManager.SelectSlot(slotIndex);
        }
    }
}
