using UnityEngine;
using System;

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;
    public Transform playerBody;

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

    float xRotation = 0f;
    float currentMouseX, currentMouseY, mouseXVelocity, mouseYVelocity;
    private bool spectatorMode = false;

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        // defaults
        if (normalPP != null) normalPP.enabled = true;
        if (spectatorPP != null) spectatorPP.enabled = false;
    }

    /// <summary>
    /// Called by Death when spectator starts.
    /// - Stops rotating the body
    /// - Enables spectator PP by switching the camera's Volume Layer Mask
    /// - Optionally toggles PP components
    /// </summary>
    public void SetSpectatorMode(bool enabled)
    {
        spectatorMode = enabled;

        if (spectatorMode)
        {
            // camera no longer controls body rotation
            playerBody = null;

            // toggle components (optional)
            if (normalPP != null) normalPP.enabled = false;
            if (spectatorPP != null) spectatorPP.enabled = true;

            // THIS is the real fix: include spectator PP layer in the camera's volume mask
            ForcePostProcessingLayerOnCamera();
        }
        else
        {
            if (normalPP != null) normalPP.enabled = true;
            if (spectatorPP != null) spectatorPP.enabled = false;
        }
    }

    void Update()
    {
        float targetMouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float targetMouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        currentMouseX = Mathf.SmoothDamp(currentMouseX, targetMouseX, ref mouseXVelocity, smoothTime);
        currentMouseY = Mathf.SmoothDamp(currentMouseY, targetMouseY, ref mouseYVelocity, smoothTime);

        xRotation -= currentMouseY * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (!spectatorMode && playerBody != null)
            playerBody.Rotate(Vector3.up * currentMouseX * Time.deltaTime);
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

        // --- URP: UniversalAdditionalCameraData.volumeLayerMask ---
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

        // --- PPv2: PostProcessLayer.volumeLayer ---
        var ppv2Type = Type.GetType(
            "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime"
        );

        if (ppv2Type != null)
        {
            var pp = cam.GetComponent(ppv2Type);
            if (pp != null)
            {
                var prop = ppv2Type.GetProperty("volumeLayer");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    LayerMask current = (LayerMask)prop.GetValue(pp);

                    LayerMask next = keepExistingVolumeMask
                        ? (LayerMask)(current.value | addMask)
                        : (LayerMask)addMask;

                    prop.SetValue(pp, next);

                    Debug.Log($"[PlayerView] PPv2 volumeLayer now: {next.value} (added {spectatorPostProcessingLayerName})");
                    return;
                }

                var field = ppv2Type.GetField("volumeLayer");
                if (field != null && field.FieldType == typeof(LayerMask))
                {
                    LayerMask current = (LayerMask)field.GetValue(pp);

                    LayerMask next = keepExistingVolumeMask
                        ? (LayerMask)(current.value | addMask)
                        : (LayerMask)addMask;

                    field.SetValue(pp, next);

                    Debug.Log($"[PlayerView] PPv2 volumeLayer(field) now: {next.value} (added {spectatorPostProcessingLayerName})");
                    return;
                }
            }
        }

        Debug.LogWarning("[PlayerView] Could not switch PP layer mask (no URP AdditionalCameraData and no PPv2 PostProcessLayer found).");
    }
}
