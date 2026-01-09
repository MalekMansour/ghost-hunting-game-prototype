using UnityEngine;

public class Interaction : MonoBehaviour
{
    [Header("Pickup")]
    public float interactDistance = 3f;
    public LayerMask itemLayer;

    [Header("Doors")]
    public LayerMask doorLayer;

    // ✅ Sinks (Interactables)
    [Header("Sinks")]
    public LayerMask sinkHandleLayer;

    [Header("Hold Point")]
    public Transform worldHoldPoint;

    [Header("Crosshairs")]
    public GameObject crosshairDefault;
    public GameObject crosshairInteract;
    public GameObject grabCrosshair;

    private Camera cam;
    private GameObject worldItem;

    private int originalLayer;
    private Collider itemCollider;

    private PlayerInventory inventory;

    private PlayerRaycaster raycaster;

    void Start()
    {
        cam = Camera.main;
        inventory = GetComponent<PlayerInventory>();
        raycaster = GetComponent<PlayerRaycaster>();

        if (crosshairDefault != null) crosshairDefault.SetActive(true);
        if (crosshairInteract != null) crosshairInteract.SetActive(false);
        if (grabCrosshair != null) grabCrosshair.SetActive(false);
    }

    void Update()
    {
        HandleCrosshair();

        // ✅ LEFT CLICK = Interact (Sink)
        if (Input.GetMouseButtonDown(0))
        {
            TrySink();
        }

        // ✅ E = Grab / Door
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Doors first, then pickup (your original behavior)
            if (!TryDoor())
                TryPickup();
        }

        if (Input.GetKeyDown(KeyCode.G))
            Drop();
    }

    public void SetHoldPoint(Transform holdPoint)
    {
        worldHoldPoint = holdPoint;
    }

    void HandleCrosshair()
    {
        if (cam == null || inventory == null)
            return;

        // Check if holding an item in hand (equipped)
        bool holdingItem = inventory.GetCurrentItem() != null;

        if (holdingItem)
        {
            InventoryItem currentItem = inventory.GetCurrentItem();
            IUseOnTarget usable = currentItem.GetComponent<IUseOnTarget>();

            if (usable != null && usable.CanUse(cam))
            {
                // Show interact crosshair if the held item can be used on something
                crosshairDefault?.SetActive(false);
                crosshairInteract?.SetActive(true);
                grabCrosshair?.SetActive(false);
                return;
            }

            // Holding an item but cannot use it
            crosshairDefault?.SetActive(true);
            crosshairInteract?.SetActive(false);
            grabCrosshair?.SetActive(false);
            return;
        }

        // Not holding anything, normal checks
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        bool hitItem = Physics.Raycast(ray, interactDistance, itemLayer);
        bool hitDoor = Physics.Raycast(ray, interactDistance, doorLayer);

        // ✅ Sink handle shows INTERACT crosshair (not grab)
        bool hitSink = Physics.Raycast(ray, interactDistance, sinkHandleLayer);

        if (hitSink)
        {
            crosshairDefault?.SetActive(false);
            crosshairInteract?.SetActive(true);
            grabCrosshair?.SetActive(false);
            return;
        }

        bool hitInteractable = hitItem || hitDoor;

        crosshairDefault?.SetActive(!hitInteractable);
        crosshairInteract?.SetActive(false);
        grabCrosshair?.SetActive(hitInteractable);
    }

    // ✅ Sink interaction (LEFT CLICK calls this)
    bool TrySink()
    {
        if (cam == null)
            return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, sinkHandleLayer))
            return false;

        Sink sink = hit.collider.GetComponentInParent<Sink>();
        if (sink == null)
            return false;

        sink.Toggle();
        return true;
    }

    // ✅ Door interaction (E calls this)
    bool TryDoor()
    {
        if (cam == null)
            return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, doorLayer))
            return false;

        Door door = hit.collider.GetComponentInParent<Door>();
        if (door == null)
            return false;

        door.Toggle();
        return true;
    }

    void TryPickup()
    {
        if (worldHoldPoint == null || inventory == null)
            return;

        // Only pick up if hands are empty
        if (!inventory.IsHandsEmpty())
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, itemLayer))
            return;

        InventoryItem item = hit.collider.GetComponent<InventoryItem>();
        if (item == null)
            return;

        // Reset dropped state so impact sound + physics work again later
        Dropped dropped = item.GetComponent<Dropped>();
        if (dropped != null)
            dropped.ResetDropped();

        // Try to add to inventory
        if (!inventory.TryAddItem(item))
            return;

        // Track held world item (for crosshair / interaction logic)
        worldItem = item.gameObject;
    }

    void PickUp(GameObject item)
    {
        worldItem = item;

        // Prevent re-pickup
        originalLayer = worldItem.layer;
        worldItem.layer = LayerMask.NameToLayer("Ignore Raycast");

        itemCollider = worldItem.GetComponent<Collider>();
        if (itemCollider != null)
            itemCollider.enabled = false;

        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (worldHoldPoint.childCount > 0)
        {
            Transform placeholder = worldHoldPoint.GetChild(0);
            worldItem.transform.SetPositionAndRotation(
                placeholder.position,
                placeholder.rotation
            );
        }

        worldItem.transform.SetParent(worldHoldPoint, true);
    }

    void Drop()
    {
        if (inventory == null)
            return;

        // Drop the currently equipped item in inventory
        InventoryItem currentItem = inventory.GetCurrentItem();
        if (currentItem == null)
            return;

        // Restore physics
        Rigidbody rb = currentItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider col = currentItem.GetComponent<Collider>();
        if (col != null)
            col.enabled = true;

        // Detach from hand
        currentItem.transform.SetParent(null);
        currentItem.gameObject.SetActive(true);

        // Remove from inventory
        inventory.DropCurrent();

        // Reset worldItem reference
        worldItem = null;
        itemCollider = null;
    }
}
