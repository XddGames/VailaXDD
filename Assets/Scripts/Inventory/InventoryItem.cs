using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public string itemName;
    public Sprite icon;
    public bool isEquipped;
    public ItemType itemType;

    public enum ItemType
    {
        Flashlight,
        // Add more item types here as needed
    }

    public InventoryItem(string name, Sprite itemIcon, ItemType type)
    {
        itemName = name;
        icon = itemIcon;
        itemType = type;
        isEquipped = false;
    }
}
