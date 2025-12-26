using UnityEngine;

public class PlayerRaycaster : MonoBehaviour
{
    [Header("Raycast")]
    public float interactDistance = 3f;
    public LayerMask itemLayer;
    public float raycastInterval = 0.1f;

    [HideInInspector] public bool hasHit;
    [HideInInspector] public RaycastHit hit;

    private Camera cam;
    private float raycastTimer;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        raycastTimer -= Time.deltaTime;

        if (raycastTimer <= 0f)
        {
            raycastTimer = raycastInterval;
            DoRaycast();
        }
    }

    void DoRaycast()
    {
        hasHit = false;

        if (cam == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit rayHit, interactDistance, itemLayer))
        {
            hasHit = true;
            hit = rayHit;
        }
    }
}

