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

    public bool TryAddItem(InventoryItem item)
    {
        if (slots[currentSlot] != null)
            return false;

        slots[currentSlot] = item;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = item.GetComponent<Collider>();
        if (col) col.enabled = false;

        item.transform.SetParent(transform);
        item.gameObject.SetActive(false);

        EquipCurrent();

        return true;
    }

    void SelectSlot(int slot)
    {
        if (slot == currentSlot)
        {
            UnequipCurrent();
            inventoryUI.ShowEmptyHand();
            return;
        }

        UnequipCurrent();
        currentSlot = slot;
        EquipCurrent();
    }

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

    public void DropCurrent()
    {
        InventoryItem item = slots[currentSlot];
        if (item == null)
            return;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Collider col = item.GetComponent<Collider>();
        if (col) col.enabled = true;

        item.transform.SetParent(null);
        item.gameObject.SetActive(true);

        Dropped dropped = item.GetComponent<Dropped>();
        if (dropped != null)
        {
            dropped.OnDropped();      
            dropped.ResetDropped();   
        }

        slots[currentSlot] = null;

        inventoryUI.ShowEmptyHand();
    }

    public InventoryItem GetCurrentItem()
    {
        return slots[currentSlot];
    }

    public bool IsHandsEmpty()
    {
        return slots[currentSlot] == null;
    }
}
