using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Rendering; // Volume

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;
    public Transform playerBody;

    [Header("Pitch Pivot (recommended)")]
    [Tooltip("Rotate this up/down (pitch). Usually an empty like 'CameraPivot' that parents the camera.")]
    public Transform pitchPivot;

    [Header("Look Limits")]
    public float maxLookUp = 90f;
    public float maxLookDown = -40f;

    [Header("Spectator Post Processing Layer")]
    [Tooltip("This is the LAYER your spectator Volume object is on.")]
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    [Tooltip("Keep normal PP layer(s) too? If true, we ADD spectator layer instead of replacing mask.")]
    public bool keepExistingVolumeMask = true;

    [Header("Optional: Component toggles (extra safety)")]
    [Tooltip("Your normal PP component/Volume (enabled while alive). Optional.")]
    public Behaviour normalPP;

    [Tooltip("Your spectator PP component/Volume (disabled by default, enabled on death). Optional.")]
    public Behaviour spectatorPP;

    [Header("URP Volume Fade (nice transition)")]
    [Tooltip("Drag your normal URP Volume here (the one currently active while alive).")]
    public Volume normalVolume;

    [Tooltip("Drag your spectator URP Volume here (the one you want after death).")]
    public Volume spectatorVolume;

    [Tooltip("How long it takes to fade from normal -> spectator.")]
    public float volumeFadeDuration = 0.6f;

    [Tooltip("If true, we will also enable spectatorVolume GameObject/Behaviour before fading in.")]
    public bool forceEnableSpectatorVolume = true;

    float xRotation = 0f;
    float currentMouseX, currentMouseY, mouseXVelocity, mouseYVelocity;
    private bool spectatorMode = false;

    private Coroutine fadeRoutine;

    // ✅ stable yaw accumulator
    private float yawRotation = 0f;

    // ✅ if body has rigidbody, rotate via MoveRotation (prevents "orbit-y" weird feel)
    private Rigidbody bodyRb;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pitchPivot == null)
            pitchPivot = transform; // backward compatible

        if (playerBody != null)
        {
            yawRotation = playerBody.eulerAngles.y;
            bodyRb = playerBody.GetComponent<Rigidbody>();
        }

        // Initialize pitch from current pivot
        float initPitch = pitchPivot.localEulerAngles.x;
        if (initPitch > 180f) initPitch -= 360f;
        xRotation = Mathf.Clamp(initPitch, maxLookDown, maxLookUp);

        // defaults
        if (normalPP != null) normalPP.enabled = true;
        if (spectatorPP != null) spectatorPP.enabled = false;

        // defaults for volumes
        if (normalVolume != null) normalVolume.weight = 1f;
        if (spectatorVolume != null)
        {
            if (forceEnableSpectatorVolume) spectatorVolume.enabled = true;
            spectatorVolume.weight = 0f;
        }
    }

    public void SetSpectatorMode(bool enabled)
    {
        spectatorMode = enabled;

        if (spectatorMode)
        {
            // camera no longer controls body rotation
            playerBody = null;
            bodyRb = null;

            if (normalPP != null) normalPP.enabled = false;
            if (spectatorPP != null) spectatorPP.enabled = true;

            ForcePostProcessingLayerOnCamera();

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeToSpectatorVolume());
        }
        else
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;

            if (normalPP != null) normalPP.enabled = true;
            if (spectatorPP != null) spectatorPP.enabled = false;

            if (normalVolume != null) normalVolume.weight = 1f;
            if (spectatorVolume != null) spectatorVolume.weight = 0f;

            // restore rb reference if body still assigned later
            if (playerBody != null) bodyRb = playerBody.GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        float targetMouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float targetMouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        currentMouseX = Mathf.SmoothDamp(currentMouseX, targetMouseX, ref mouseXVelocity, smoothTime);
        currentMouseY = Mathf.SmoothDamp(currentMouseY, targetMouseY, ref mouseYVelocity, smoothTime);

        // Pitch (camera only)
        xRotation -= currentMouseY * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);

        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        else
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Yaw accumulator (apply in FixedUpdate if using Rigidbody)
        if (!spectatorMode && playerBody != null)
        {
            yawRotation += currentMouseX * Time.deltaTime;

            // If no rigidbody, fall back to transform rotation (still shouldn't change position)
            if (bodyRb == null)
                playerBody.rotation = Quaternion.Euler(0f, yawRotation, 0f);
        }
    }

    void FixedUpdate()
    {
        // Apply yaw through Rigidbody for physics correctness
        if (!spectatorMode && playerBody != null && bodyRb != null)
        {
            Quaternion target = Quaternion.Euler(0f, yawRotation, 0f);
            bodyRb.MoveRotation(target);
        }
    }

    private IEnumerator FadeToSpectatorVolume()
    {
        if (normalVolume == null || spectatorVolume == null)
            yield break;

        if (forceEnableSpectatorVolume)
            spectatorVolume.enabled = true;

        float duration = Mathf.Max(0.01f, volumeFadeDuration);

        float startNormal = normalVolume.weight;
        float startSpec = spectatorVolume.weight;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            float lerp = Mathf.Clamp01(t);
            normalVolume.weight = Mathf.Lerp(startNormal, 0f, lerp);
            spectatorVolume.weight = Mathf.Lerp(startSpec, 1f, lerp);

            yield return null;
        }

        normalVolume.weight = 0f;
        spectatorVolume.weight = 1f;
    }

    private void ForcePostProcessingLayerOnCamera()
    {
        int layer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[PlayerView] Layer '{spectatorPostProcessingLayerName}' does not exist.");
            return;
        }

        int addMask = (1 << layer);
        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("[PlayerView] No Camera component found on this object.");
            return;
        }

        var urpType = Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"
        );

        if (urpType != null)
        {
            var urp = cam.GetComponent(urpType);
            if (urp != null)
            {
                var prop = urpType.GetProperty("volumeLayerMask");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    LayerMask current = (LayerMask)prop.GetValue(urp);

                    LayerMask next = keepExistingVolumeMask
                        ? (LayerMask)(current.value | addMask)
                        : (LayerMask)addMask;

                    prop.SetValue(urp, next);

                    Debug.Log($"[PlayerView] URP volumeLayerMask now: {next.value} (added {spectatorPostProcessingLayerName})");
                    return;
                }
            }
        }

        Debug.LogWarning("[PlayerView] Could not switch PP layer mask (URP AdditionalCameraData not found).");
    }
}
