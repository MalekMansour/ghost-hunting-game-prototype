using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory")]
    public int maxSlots = 4;

    [Header("Hand")]
    public Transform handPoint;

    [Header("UI")]
    public InventoryUI inventoryUI;

    private InventoryItem[] slots;
    private int currentSlot = 0;

    void Awake()
    {
        slots = new InventoryItem[maxSlots];
    }

    void Start()
    {
        SelectSlot(0);
    }

    void Update()
    {
        // Cycle slots
        if (Input.GetKeyDown(KeyCode.Q))
            SelectSlot((currentSlot + 1) % maxSlots);

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
    }

    // ---------------- ADD ITEM ----------------
    public bool TryAddItem(InventoryItem item)
    {
        // Only add if current slot empty
        if (slots[currentSlot] != null)
            return false;

        slots[currentSlot] = item;

        // Disable physics & collider
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = item.GetComponent<Collider>();
        if (col) col.enabled = false;

        // Parent to inventory root and deactivate
        item.transform.SetParent(transform);
        item.gameObject.SetActive(false);

        // Equip immediately
        EquipCurrent();

        return true;
    }

    // ---------------- SLOT SWITCH ----------------
    void SelectSlot(int slot)
    {
        if (slot == currentSlot)
        {
            // Toggle current slot OFF if selected again
            UnequipCurrent();
            inventoryUI.ShowEmptyHand();
            return;
        }

        UnequipCurrent();
        currentSlot = slot;
        EquipCurrent();
    }

    // ---------------- EQUIP / UNEQUIP ----------------
    void EquipCurrent()
    {
        InventoryItem item = slots[currentSlot];

        if (item == null)
        {
            inventoryUI.ShowEmptyHand();
            return;
        }

        item.gameObject.SetActive(true);
        item.OnEquip(handPoint);
        inventoryUI.SetItem(item.icon);
    }

    void UnequipCurrent()
    {
        InventoryItem item = slots[currentSlot];
        if (item == null)
            return;

        item.OnUnequip();
        item.gameObject.SetActive(false);
    }

    // ---------------- DROP ITEM ----------------
    public void DropCurrent()
    {
        InventoryItem item = slots[currentSlot];
        if (item == null)
            return;

        // Restore physics
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Collider col = item.GetComponent<Collider>();
        if (col) col.enabled = true;

        // Detach from inventory
        item.transform.SetParent(null);
        item.gameObject.SetActive(true);

        // Play drop sound if the item has a Dropped component
        Dropped dropped = item.GetComponent<Dropped>();
        if (dropped != null)
        {
            dropped.OnDropped();      // plays sound
            dropped.ResetDropped();   // ensures it can play again on future drops
        }

        // Remove from inventory
        slots[currentSlot] = null;

        // Reset hand UI
        inventoryUI.ShowEmptyHand();
    }

    // ---------------- HELPERS ----------------
    public InventoryItem GetCurrentItem()
    {
        return slots[currentSlot];
    }

    public bool IsHandsEmpty()
    {
        return slots[currentSlot] == null;
    }
}

