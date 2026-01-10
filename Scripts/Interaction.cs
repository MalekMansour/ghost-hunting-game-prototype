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

    // ✅ NEW: Generic Interactables (Layer = Interactable)
    [Header("Generic Interactables")]
    public LayerMask interactableLayer;

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

        // ✅ LEFT CLICK = Interact (Sink + Generic Interactables)
        if (Input.GetMouseButtonDown(0))
        {
            // Try sinks first (specific)
            if (TrySink())
                return;

            // Then generic interactables
            TryGenericInteractable();
        }

        // ✅ E = Door / Pickup (unchanged)
        if (Input.GetKeyDown(KeyCode.E))
        {
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

        // Holding item logic (unchanged)
        bool holdingItem = inventory.GetCurrentItem() != null;

        if (holdingItem)
        {
            InventoryItem currentItem = inventory.GetCurrentItem();
            IUseOnTarget usable = currentItem.GetComponent<IUseOnTarget>();

            if (usable != null && usable.CanUse(cam))
            {
                crosshairDefault?.SetActive(false);
                crosshairInteract?.SetActive(true);
                grabCrosshair?.SetActive(false);
                return;
            }

            crosshairDefault?.SetActive(true);
            crosshairInteract?.SetActive(false);
            grabCrosshair?.SetActive(false);
            return;
        }

        // Not holding anything: check what we're looking at
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        bool hitItem = Physics.Raycast(ray, interactDistance, itemLayer);
        bool hitDoor = Physics.Raycast(ray, interactDistance, doorLayer);

        // ✅ Sink handle shows INTERACT crosshair
        bool hitSink = Physics.Raycast(ray, interactDistance, sinkHandleLayer);

        // ✅ NEW: Any object on Interactable layer shows INTERACT crosshair
        bool hitGenericInteract = Physics.Raycast(ray, interactDistance, interactableLayer);

        if (hitSink || hitGenericInteract)
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

    // ✅ NEW: Generic interactable interaction (LEFT CLICK calls this)
    bool TryGenericInteractable()
    {
        if (cam == null)
            return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            return false;

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null)
            return false;

        interactable.Interact();
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

        if (!inventory.IsHandsEmpty())
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, itemLayer))
            return;

        InventoryItem item = hit.collider.GetComponent<InventoryItem>();
        if (item == null)
            return;

        Dropped dropped = item.GetComponent<Dropped>();
        if (dropped != null)
            dropped.ResetDropped();

        if (!inventory.TryAddItem(item))
            return;

        worldItem = item.gameObject;
    }

    void PickUp(GameObject item)
    {
        worldItem = item;

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

        InventoryItem currentItem = inventory.GetCurrentItem();
        if (currentItem == null)
            return;

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

        currentItem.transform.SetParent(null);
        currentItem.gameObject.SetActive(true);

        inventory.DropCurrent();

        worldItem = null;
        itemCollider = null;
    }
}
