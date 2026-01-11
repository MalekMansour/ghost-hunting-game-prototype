using UnityEngine;

public class DoorLock : MonoBehaviour
{
    [Header("Lock State")]
    [Tooltip("If true, door starts locked.")]
    public bool startLocked = true;

    [Tooltip("If true, door locks automatically during hunts.")]
    public bool lockDuringHunt = false;

    [Header("Key Door Settings")]
    [Tooltip("If empty, this door does NOT require a key.")]
    public string requiredKeyId;

    private bool isLocked;
    private Door door;

    void Awake()
    {
        door = GetComponent<Door>();
        isLocked = startLocked;
    }

    // Called by Interaction.cs
    public bool TryOpenOrToggle()
    {
        if (isLocked)
            return false;

        door.Toggle();
        return true;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    // Used for shortcut doors
    public bool PlayerHasValidKeyInHand(PlayerInventory inventory)
    {
        if (inventory == null)
            return false;

        InventoryItem item = inventory.GetCurrentItem();
        if (item == null)
            return false;

        KeyItem key = item.GetComponent<KeyItem>();
        if (key == null)
            return false;

        return key.keyId == requiredKeyId;
    }

    // Called when player LEFT CLICKS with key
    public void UnlockWithKey(PlayerInventory inventory)
    {
        if (!PlayerHasValidKeyInHand(inventory))
            return;

        isLocked = false;
    }

    // 🔴 These are for your ghost system later
    public void Lock()
    {
        isLocked = true;
    }

    public void Unlock()
    {
        isLocked = false;
    }
}
