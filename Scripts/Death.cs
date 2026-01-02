using System.Collections;
using UnityEngine;
using UnityEngine.Rendering; // Volume

public class Death : MonoBehaviour
{
    [Header("Ghost Spectator Mode")]
    public bool enableGhostMode = true;

    [Tooltip("Optional: set this tag when dead (leave empty to not change).")]
    public string ghostTag = "GhostSpectator";

    [Tooltip("Layer name to apply to the player hierarchy when dead.")]
    public string ghostLayerName = "GhostSpectator";

    [Header("Disable These On Death")]
    [Tooltip("Drag scripts like Interact, Pickup, Inventory, FlashlightToggle, etc. Leave movement scripts OUT so you can still walk/run/crouch.")]
    public Behaviour[] disableOnDeath;

    [Tooltip("Optional: disable objects like hands/items/UI prompts on death.")]
    public GameObject[] disableObjectsOnDeath;

    [Header("Ghost Vision")]
    public Camera playerCamera;

    [Tooltip("If true, we swap camera culling mask when dead.")]
    public bool overrideCameraCullingMask = false;
    public LayerMask ghostCullingMask;

    [Tooltip("Optional: player Volume (post processing). If null, we auto-find under camera.")]
    public Volume postProcessVolume;

    [Tooltip("Optional: ghost vision profile (bright/clear). If null, we just crank weight.")]
    public VolumeProfile ghostVisionProfile;

    [Range(0f, 1f)]
    public float ghostVisionWeight = 1f;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool hasDied = false;

    private string cachedTag;
    private int cachedLayer;
    private int cachedGhostLayer;

    private int cachedCameraMask;

    private float cachedVolumeWeight;
    private VolumeProfile cachedVolumeProfile;

    void Awake()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (postProcessVolume == null && playerCamera != null)
            postProcessVolume = playerCamera.GetComponentInChildren<Volume>(true);

        if (playerCamera != null)
            cachedCameraMask = playerCamera.cullingMask;

        if (postProcessVolume != null)
        {
            cachedVolumeWeight = postProcessVolume.weight;
            cachedVolumeProfile = postProcessVolume.profile;
        }

        cachedLayer = gameObject.layer;
        cachedTag = gameObject.tag;
    }

    public void BeginDeath(float delay)
    {
        if (hasDied) return;
        StartCoroutine(DeathRoutine(Mathf.Max(0f, delay)));
    }

    IEnumerator DeathRoutine(float delay)
    {
        hasDied = true;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!enableGhostMode)
        {
            Log("Ghost mode disabled; stopping after death.");
            yield break;
        }

        // Disable gameplay interaction scripts
        if (disableOnDeath != null)
        {
            for (int i = 0; i < disableOnDeath.Length; i++)
                if (disableOnDeath[i] != null) disableOnDeath[i].enabled = false;
        }

        // Disable objects like hands / items / prompts
        if (disableObjectsOnDeath != null)
        {
            for (int i = 0; i < disableObjectsOnDeath.Length; i++)
                if (disableObjectsOnDeath[i] != null) disableObjectsOnDeath[i].SetActive(false);
        }

        // Tag swap (optional)
        if (!string.IsNullOrEmpty(ghostTag))
            gameObject.tag = ghostTag;

        // Layer swap (recommended so ghosts/players don't "see" you via layers)
        cachedGhostLayer = LayerMask.NameToLayer(ghostLayerName);
        if (!string.IsNullOrEmpty(ghostLayerName) && cachedGhostLayer != -1)
            SetLayerRecursively(gameObject, cachedGhostLayer);
        else
            Log($"Ghost layer '{ghostLayerName}' not found. Create it in Unity > Layers.");

        // Ghost vision: camera mask swap (optional)
        if (playerCamera != null && overrideCameraCullingMask)
        {
            cachedCameraMask = playerCamera.cullingMask;
            playerCamera.cullingMask = ghostCullingMask;
        }

        // Ghost vision: post processing
        if (postProcessVolume != null)
        {
            cachedVolumeWeight = postProcessVolume.weight;
            cachedVolumeProfile = postProcessVolume.profile;

            if (ghostVisionProfile != null)
                postProcessVolume.profile = ghostVisionProfile;

            postProcessVolume.weight = ghostVisionWeight;
        }

        Log("Ghost spectator mode enabled.");
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;

        Transform t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log("[Death] " + msg, this);
    }

    public bool IsDead() => hasDied;
}
