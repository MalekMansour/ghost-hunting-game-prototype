using UnityEngine;
using System;
using System.Reflection;

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;
    public Transform playerBody;

    [Header("Look Limits")]
    public float maxLookUp = 90f;
    public float maxLookDown = -40f;

    [Header("Post Processing Layer Switch")]
    [Tooltip("Layer that spectator post processing volumes are on.")]
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    float xRotation = 0f;

    float currentMouseX;
    float currentMouseY;
    float mouseXVelocity;
    float mouseYVelocity;

    private bool spectatorMode = false;

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    /// <summary>
    /// Called by Death when spectator starts.
    /// - Stops rotating the player body
    /// - Switches post processing volume mask to spectator layer
    /// </summary>
    public void SetSpectatorMode(bool enabled)
    {
        spectatorMode = enabled;

        if (spectatorMode)
        {
            // camera no longer controls the body
            playerBody = null;

            // switch post processing
            SwitchCameraPostProcessingLayer();
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

        // Only rotate player body if NOT in spectator
        if (!spectatorMode && playerBody != null)
        {
            playerBody.Rotate(Vector3.up * currentMouseX * Time.deltaTime);
        }
    }

    private void SwitchCameraPostProcessingLayer()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        int toLayer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);
        if (toLayer < 0)
        {
            Debug.LogWarning($"[PlayerView] Layer '{spectatorPostProcessingLayerName}' not found. Create it.");
            return;
        }

        int spectatorMask = (1 << toLayer);

        // URP: UniversalAdditionalCameraData.volumeLayerMask
        Type urpType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null)
        {
            var urp = cam.GetComponent(urpType);
            if (urp != null)
            {
                var prop = urpType.GetProperty("volumeLayerMask");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    prop.SetValue(urp, (LayerMask)spectatorMask);
                    Debug.Log($"[PlayerView] URP volumeLayerMask -> '{spectatorPostProcessingLayerName}'");
                    return;
                }
            }
        }

        // PPv2: PostProcessLayer.volumeLayer
        Type ppv2Type = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
        if (ppv2Type != null)
        {
            var pp = cam.GetComponent(ppv2Type);
            if (pp != null)
            {
                var prop = ppv2Type.GetProperty("volumeLayer");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    prop.SetValue(pp, (LayerMask)spectatorMask);
                    Debug.Log($"[PlayerView] PPv2 volumeLayer -> '{spectatorPostProcessingLayerName}'");
                    return;
                }

                var field = ppv2Type.GetField("volumeLayer");
                if (field != null && field.FieldType == typeof(LayerMask))
                {
                    field.SetValue(pp, (LayerMask)spectatorMask);
                    Debug.Log($"[PlayerView] PPv2 volumeLayer(field) -> '{spectatorPostProcessingLayerName}'");
                    return;
                }
            }
        }

        Debug.LogWarning("[PlayerView] No post-processing component found to switch (URP AdditionalCameraData or PPv2 PostProcessLayer).");
    }
}
