using UnityEngine;

public class Interaction : MonoBehaviour
{
    [Header("Pickup")]
    public float interactDistance = 3f;
    public LayerMask itemLayer;

    [Header("Hold Point")]
    public Transform worldHoldPoint;

    [Header("Crosshairs")]
    public GameObject crosshairDefault;
    public GameObject crosshairInteract;

    private Camera cam;
    private GameObject worldItem;

    private int originalLayer; // store item layer

    void Start()
    {
        cam = Camera.main;

        if (crosshairDefault != null) crosshairDefault.SetActive(true);
        if (crosshairInteract != null) crosshairInteract.SetActive(false);
    }

    void Update()
    {
        HandleCrosshair();

        if (Input.GetKeyDown(KeyCode.E))
            TryPickup();

        if (Input.GetKeyDown(KeyCode.G))
            Drop();
    }

    public void SetHoldPoint(Transform holdPoint)
    {
        worldHoldPoint = holdPoint;
    }

    void HandleCrosshair()
    {
        if (cam == null || worldItem != null)
        {
            crosshairDefault.SetActive(true);
            crosshairInteract.SetActive(false);
            return;
        }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        bool hitItem = Physics.Raycast(ray, interactDistance, itemLayer);

        crosshairDefault.SetActive(!hitItem);
        crosshairInteract.SetActive(hitItem);
    }

    void TryPickup()
    {
        if (worldItem != null || worldHoldPoint == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, itemLayer))
        {
            PickUp(hit.collider.gameObject);
        }
    }

    void PickUp(GameObject item)
    {
        worldItem = item;

        // store & change layer so it can't be raycast
        originalLayer = worldItem.layer;
        worldItem.layer = LayerMask.NameToLayer("Ignore Raycast");

        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (worldHoldPoint.childCount > 0)
        {
            Transform placeholder = worldHoldPoint.GetChild(0);
            worldItem.transform.position = placeholder.position;
            worldItem.transform.rotation = placeholder.rotation;
        }

        worldItem.transform.SetParent(worldHoldPoint);
    }

    void Drop()
    {
        if (worldItem == null)
            return;

        worldItem.transform.SetParent(null);

        // restore layer
        worldItem.layer = originalLayer;

        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        worldItem = null;
    }
}
