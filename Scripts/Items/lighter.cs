using UnityEngine;

public class Lighter : MonoBehaviour, IUseOnTarget
{
    [Header("Input")]
    public KeyCode useKey = KeyCode.Mouse1;
    public KeyCode interactKey = KeyCode.Mouse0;

    [Header("Flame")]
    public GameObject flameObject;
    public Light flameLight;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip lighterOnSound;

    [Header("Candle Interaction")]
    public float candleInteractDistance = 2f;

    [Header("Animation")]
    public string useItemTrigger = "UseItem";

    [Header("Cooldown")]
    public float toggleCooldown = 1f;

    private bool isLit = false;
    private float lastToggleTime = -10f;
    private Camera cam;
    private Animator playerAnimator;

    void Start()
    {
        cam = Camera.main;
        SetFlame(false);
    }

    void Update()
    {
        if (!IsHeldByPlayer()) return;

        if (playerAnimator == null)
            playerAnimator = GetComponentInParent<Animator>();

        if (Input.GetKeyDown(useKey))
            TryToggleLighter();

        if (isLit && Input.GetKeyDown(interactKey))
            TryLightCandle();
    }

    bool IsHeldByPlayer()
    {
        if (transform.parent == null) return false;
        string parentName = transform.parent.name.ToLower();
        return parentName.Contains("hand") || parentName.Contains("holdpoint");
    }

    void TryToggleLighter()
    {
        if (Time.time - lastToggleTime < toggleCooldown) return;
        lastToggleTime = Time.time;

        bool wasLit = isLit;
        isLit = !isLit;
        SetFlame(isLit);

        if (!wasLit && isLit)
        {
            if (audioSource && lighterOnSound)
                audioSource.PlayOneShot(lighterOnSound);

            PlayUseAnimation();
        }
    }

    void SetFlame(bool state)
    {
        if (flameObject) flameObject.SetActive(state);
        if (flameLight) flameLight.enabled = state;
    }

    void PlayUseAnimation()
    {
        if (playerAnimator)
            playerAnimator.SetTrigger(useItemTrigger);
    }

    void TryLightCandle()
    {
        if (cam == null) return;
        if (!isLit) return; // only usable if lighter is on

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, candleInteractDistance))
        {
            Candle candle = hit.collider.GetComponent<Candle>();
            if (candle != null)
            {
                candle.LightCandle();
                PlayUseAnimation();
            }
        }
    }

    public bool CanUse(Camera cam)
    {
        if (!isLit || cam == null) return false;
        if (!IsHeldByPlayer()) return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, candleInteractDistance))
        {
            return hit.collider.GetComponent<Candle>() != null;
        }

        return false;
    }
}
