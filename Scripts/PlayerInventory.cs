using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory")]
    public int maxSlots = 4;

    [Header("Hand")]
    public Transform handPoint;

    [Header("UI")]
    public InventoryUI inventoryUI;

    // ----------------------------
    // ADDED (Death Drop settings)
    // ----------------------------
    [Header("Death Drop (Optional)")]
    public float deathThrowForwardForce = 3.5f;
    public float deathThrowUpForce = 1.2f;
    public float deathSpawnForwardOffset = 0.6f;
    public float deathSpawnUpOffset = 0.2f;
    public float deathSideScatter = 0.25f; // small spread so items don't stack

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

    // ✅ ADDED: Drop everything safely without breaking existing logic.
    // (Keeps your old behavior intact, just drops all slots.)
    public void DropAllItems()
    {
        int prevSlot = currentSlot;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            currentSlot = i;
            UnequipCurrent();
            DropCurrent();
        }

        currentSlot = prevSlot;
        inventoryUI.ShowEmptyHand();
    }

    // ✅ ADDED: Drop EVERYTHING for death, with a forward throw.
    // This DOES NOT rely on currentSlot cycling, so it's safer at death time.
    public void DropAllItemsOnDeath(Transform throwFrom)
    {
        if (throwFrom == null) throwFrom = transform;

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem item = slots[i];
            if (item == null) continue;

            // Ensure visible
            item.gameObject.SetActive(true);

            // Detach to world
            item.transform.SetParent(null, true);

            // Enable physics + collider
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Collider col = item.GetComponent<Collider>();
            if (col) col.enabled = true;

            // Place in front with slight scatter so items don't overlap perfectly
            Vector3 scatter = throwFrom.right * Random.Range(-deathSideScatter, deathSideScatter);
            Vector3 spawnPos =
                throwFrom.position +
                throwFrom.forward * deathSpawnForwardOffset +
                Vector3.up * deathSpawnUpOffset +
                scatter;

            item.transform.position = spawnPos;

            // Same dropped logic as DropCurrent
            Dropped dropped = item.GetComponent<Dropped>();
            if (dropped != null)
            {
                dropped.OnDropped();
                dropped.ResetDropped();
            }

            // Throw forward + a bit up, with a little spin
            if (rb)
            {
                Vector3 throwVel =
                    throwFrom.forward * deathThrowForwardForce +
                    Vector3.up * deathThrowUpForce;

                rb.AddForce(throwVel, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
            }

            // Remove from inventory
            slots[i] = null;
        }

        inventoryUI.ShowEmptyHand();
    }
}
