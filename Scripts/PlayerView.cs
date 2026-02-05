using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Rendering; 

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;

    [Header("Player Root (Yaw)")]
    [Tooltip("This rotates left/right (yaw). Set this to your Player ROOT transform.")]
    public Transform playerBody;

    [Header("Pitch Pivot (recommended)")]
    [Tooltip("Rotate this up/down (pitch). Usually an empty like 'CameraPivot' that parents the camera.")]
    public Transform pitchPivot;

    [Header("Look Limits")]
    public float maxLookUp = 90f;
    public float maxLookDown = -40f;

    [Header("Spectator Post Processing Layer")]
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";
    public bool keepExistingVolumeMask = true;

    [Header("Optional: Component toggles (extra safety)")]
    public Behaviour normalPP;
    public Behaviour spectatorPP;

    [Header("URP Volume Fade (nice transition)")]
    public Volume normalVolume;
    public Volume spectatorVolume;
    public float volumeFadeDuration = 0.6f;
    public bool forceEnableSpectatorVolume = true;

    // smoothed inputs
    float xRotation = 0f; // pitch
    float currentMouseX, currentMouseY, mouseXVelocity, mouseYVelocity;

    private bool spectatorMode = false;
    private Coroutine fadeRoutine;

    // yaw accumulator
    private float yawRotation = 0f;

    private Rigidbody bodyRb;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody == null)
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name == "Player") { playerBody = t; break; }
                t = t.parent;
            }

            // If your Player root isn't literally named "Player", fallback to topmost parent.
            if (playerBody == null)
            {
                Transform top = transform;
                while (top.parent != null) top = top.parent;
                playerBody = top;
            }
        }

        if (pitchPivot == null && playerBody != null)
        {
            Transform found = playerBody.Find("CameraPivot");
            if (found != null) pitchPivot = found;
        }

        // If still missing (misnamed pivot), last resort: use current parent,
        // but ONLY if it is not the camera itself.
        if (pitchPivot == null && transform.parent != null && transform.parent != transform)
            pitchPivot = transform.parent;

        if (playerBody != null)
        {
            yawRotation = playerBody.eulerAngles.y;
            bodyRb = playerBody.GetComponent<Rigidbody>();
        }

        float initPitch = 0f;
        if (pitchPivot != null)
        {
            initPitch = pitchPivot.localEulerAngles.x;
            if (initPitch > 180f) initPitch -= 360f;
        }
        xRotation = Mathf.Clamp(initPitch, maxLookDown, maxLookUp);

        // ----------------------------
        // POST PROCESS INIT
        // ----------------------------
        if (normalPP != null) normalPP.enabled = true;
        if (spectatorPP != null) spectatorPP.enabled = false;

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
            // stop controlling body rotation
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
        // Smooth mouse input
        float targetMouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float targetMouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        currentMouseX = Mathf.SmoothDamp(currentMouseX, targetMouseX, ref mouseXVelocity, smoothTime);
        currentMouseY = Mathf.SmoothDamp(currentMouseY, targetMouseY, ref mouseYVelocity, smoothTime);

        // -------------------------
        // PITCH (CameraPivot ONLY)
        // -------------------------
        xRotation -= currentMouseY * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);

        if (pitchPivot != null)
        {
            // ONLY pivot rotates in pitch; camera never rotates in this script
            pitchPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // -------------------------
        // YAW (Player ROOT ONLY)
        // -------------------------
        if (!spectatorMode && playerBody != null)
        {
            yawRotation += currentMouseX * Time.deltaTime;

            // If no Rigidbody, rotate transform directly (spin in place)
            if (bodyRb == null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yawRotation, 0f);
            }
            // Rigidbody rotation applied in FixedUpdate
        }
    }

    void FixedUpdate()
    {
        // Apply yaw through Rigidbody so the Player ROOT spins in place without physics weirdness
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
                    return;
                }
            }
        }

        Debug.LogWarning("[PlayerView] Could not switch PP layer mask (URP AdditionalCameraData not found).");
    }
}
