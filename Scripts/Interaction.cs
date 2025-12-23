using UnityEngine;
using UnityEngine.UI;

public class Interaction : MonoBehaviour
{
    [Header("Pickup")]
    public float interactDistance = 3f;
    public LayerMask itemLayer;

    [Header("Hold Point")]
    public Transform worldHoldPoint;   // character hand (runtime)

    [Header("Crosshairs")]
    public GameObject crosshairDefault;    // assign the default crosshair UI
    public GameObject crosshairInteract;   // assign the interact crosshair UI

    private Camera cam;
    private GameObject worldItem;

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
        Debug.Log("World HoldPoint set: " + holdPoint.name);
    }

    void HandleCrosshair()
    {
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        bool hitItem = Physics.Raycast(ray, out RaycastHit hit, interactDistance, itemLayer);

        if (crosshairDefault != null && crosshairInteract != null)
        {
            crosshairDefault.SetActive(!hitItem);
            crosshairInteract.SetActive(hitItem);
        }
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
        else
        {
            Debug.Log("No item hit");
        }
    }

    void PickUp(GameObject item)
    {
        worldItem = item;
        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Move item to placeholder position (placeholder stays invisible)
        if (worldHoldPoint.childCount > 0)
        {
            Transform placeholder = worldHoldPoint.GetChild(0);
            worldItem.transform.position = placeholder.position;
            worldItem.transform.rotation = placeholder.rotation;
        }

        worldItem.transform.SetParent(worldHoldPoint);

        Debug.Log("Picked up item: " + worldItem.name);
    }

    void Drop()
    {
        if (worldItem == null)
            return;

        worldItem.transform.SetParent(null);

        Rigidbody rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        worldItem = null;
        Debug.Log("Dropped item");
    }
}

