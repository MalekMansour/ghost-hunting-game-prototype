using UnityEngine;
using System.Collections;
using System.Reflection;

public class Death : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your PLAYER CYLINDER here (the object that has ALL the player scripts). If empty, uses transform.root.")]
    public GameObject playerCylinder;

    [Tooltip("Animator on the model (usually a child). Leave empty: will auto-find at death time.")]
    public Animator animator;

    [Tooltip("Root transform of the model (where ragdoll bones live). Leave empty: will try animator.transform.")]
    public Transform modelRoot;

    public AudioSource audioSource;

    [Tooltip("Main camera (child of player at runtime). Leave empty: Camera.main.")]
    public Camera mainCamera;

    [Header("Death Animation Param")]
    public string dieParamName = "Die";
    public enum DieParamMode { Trigger, Bool }
    public DieParamMode dieParamMode = DieParamMode.Trigger;
    public bool dieBoolValue = true;

    [Header("Death Timing")]
    [Tooltip("How long after death trigger before spectator activates.")]
    public float spectatorDelay = 3f;

    [Tooltip("How long after death trigger before ragdoll turns on (0.3–1.5 recommended).")]
    public float ragdollDelay = 0.75f;

    [Tooltip("If true, root motion is enabled while the death animation starts.")]
    public bool enableRootMotionDuringDeath = true;

    [Header("Audio")]
    public AudioClip deathClip;

    [Header("Layers")]
    public string spectatorLayerName = "Spectator";
    public string postProcessingLayerName = "PostProcessing";
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    [Header("Disable scripts")]
    [Tooltip("Disables ALL scripts on Cylinder (every MonoBehaviour except Death until the end).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Also disable camera scripts (ex: PlayerView) when spectator starts.")]
    public bool disableAllCameraScripts = true;

    [Tooltip("Disable Death script too at the very end.")]
    public bool disableDeathScriptToo = true;

    [Header("Spectator Movement")]
    public float spectatorControllerHeight = 1.8f;
    public float spectatorControllerRadius = 0.35f;
    public float spectatorMoveSpeed = 5f;
    public float spectatorSprintMultiplier = 1.75f;
    public float spectatorLookSensitivity = 2.2f;
    public bool lockCursor = true;

    [Header("Spectator Camera")]
    public bool keepCameraWorldY = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deadStarted;

    // cached ragdoll parts
    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private Collider[] nonRagdollColliders;

    private void Awake()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;
        if (mainCamera == null) mainCamera = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void BeginDeath()
    {
        if (deadStarted) return;
        deadStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        EnsureRuntimeReferences();

        if (animator == null)
        {
            Log("ERROR: Animator not found at death time. Make sure the spawned player prefab contains an Animator under the player root.");
            yield break;
        }

        // Start death animation
        animator.enabled = true;
        animator.applyRootMotion = enableRootMotionDuringDeath;
        animator.speed = 1f;

        if (dieParamMode == DieParamMode.Trigger)
        {
            animator.ResetTrigger(dieParamName);
            animator.SetTrigger(dieParamName);
        }
        else
        {
            animator.SetBool(dieParamName, dieBoolValue);
        }

        // Sound
        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip);

        // Wait a bit, then ragdoll
        if (ragdollDelay > 0f)
            yield return new WaitForSeconds(ragdollDelay);

        EnableRagdoll();

        // Wait remaining time until spectator starts
        float remaining = Mathf.Max(0f, spectatorDelay - ragdollDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // Switch all player layers to Spectator
        ForceRootAndCylinderToSpectatorLayer();

        // Detach camera completely + disable camera scripts that still rotate the body
        EnterSpectator();

        // Finally disable Death too if desired
        if (disableDeathScriptToo)
            this.enabled = false;
    }

    private void EnsureRuntimeReferences()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;
        if (mainCamera == null) mainCamera = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Re-acquire animator right now (spawner-safe)
        if (animator == null)
            animator = transform.root.GetComponentInChildren<Animator>(true);

        if (modelRoot == null && animator != null)
            modelRoot = animator.transform;

        // Cache ragdoll parts once
        if (modelRoot != null && ragdollBodies == null)
        {
            ragdollBodies = modelRoot.GetComponentsInChildren<Rigidbody>(true);
            ragdollColliders = modelRoot.GetComponentsInChildren<Collider>(true);

            // Colliders on the cylinder/root that you probably want OFF after ragdoll
            nonRagdollColliders = playerCylinder.GetComponentsInChildren<Collider>(true);

            // Start with ragdoll OFF (safe even if already off)
            SetRagdollState(enabled: false);
        }
    }

    // --- RAGDOLL ---
    private void EnableRagdoll()
    {
        if (modelRoot == null || ragdollBodies == null)
        {
            Log("Ragdoll skipped: modelRoot / ragdoll bodies not found.");
            return;
        }

        // Stop animator so it doesn't fight physics
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = false;
        }

        SetRagdollState(enabled: true);

        // Disable the cylinder colliders so your capsule doesn't keep pushing the ragdoll
        // (we keep the corpse visible because ragdoll bones now have colliders)
        if (nonRagdollColliders != null)
        {
            foreach (var c in nonRagdollColliders)
            {
                if (c == null) continue;

                // Keep colliders that belong to the ragdoll hierarchy ON
                if (modelRoot != null && c.transform.IsChildOf(modelRoot)) continue;

                c.enabled = false;
            }
        }

        Log("Ragdoll ENABLED.");
    }

    private void SetRagdollState(bool enabled)
    {
        // Rigidbody: enabled ragdoll => non-kinematic
        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null) continue;

                // Skip the root rigidbody if your rig has one that shouldn't simulate (rare)
                rb.isKinematic = !enabled;
                rb.detectCollisions = enabled;
            }
        }

        // Colliders: enable ragdoll colliders
        if (ragdollColliders != null)
        {
            foreach (var col in ragdollColliders)
            {
                if (col == null) continue;

                // Many rigs include the capsule collider as well; we disable non-ragdoll above
                col.enabled = enabled;
            }
        }
    }

    // --- LAYERS ---
    private void ForceRootAndCylinderToSpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorLayerName}' not found. Create it in Unity Layers.");
            return;
        }

        // Force ALL of it to spectator so nothing remains on Player layer
        SetLayerRecursively(transform.root.gameObject, specLayer);
        if (playerCylinder != null) SetLayerRecursively(playerCylinder, specLayer);

        Log($"Forced Root + Cylinder to layer '{spectatorLayerName}'.");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // --- SPECTATOR ---
    private void EnterSpectator()
    {
        if (mainCamera == null)
        {
            Log("ERROR: mainCamera missing.");
            return;
        }

        // Switch camera post processing layer mask FIRST (before detaching)
        SwitchCameraPostProcessingLayer(mainCamera);

        // Disable ALL scripts on cylinder (movement, interaction, etc.)
        if (disableAllScriptsOnCylinder)
            DisableAllScriptsOnCylinder();

        // Disable camera scripts like PlayerView so it stops rotating the corpse
        if (disableAllCameraScripts)
            DisableAllScriptsOnCamera(mainCamera);

        // FULL DETACH: camera should have NOTHING to do with the body now
        Transform camT = mainCamera.transform;
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        // Create spectator controller in world, NOT parented to player
        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camWorldPos;
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0) SetLayerRecursively(spec, specLayer);

        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        // Parent camera to spectator controller (camera is no longer under player cylinder)
        camT.SetParent(spec.transform, true);
        camT.position = camWorldPos;
        camT.rotation = camWorldRot;

        SpectatorController controller = spec.AddComponent<SpectatorController>();
        controller.cc = cc;
        controller.cameraTransform = camT;
        controller.moveSpeed = spectatorMoveSpeed;
        controller.sprintMultiplier = spectatorSprintMultiplier;
        controller.lookSensitivity = spectatorLookSensitivity;
        controller.lockCursor = lockCursor;
        controller.keepWorldY = keepCameraWorldY;
        controller.fixedWorldY = camWorldPos.y;

        Log("Spectator entered: camera detached from player completely.");
    }

    private void DisableAllScriptsOnCylinder()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;

        // Disable every MonoBehaviour on the cylinder (but keep Death alive until the end of EnterSpectator)
        var all = playerCylinder.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb == this) continue;
            mb.enabled = false;
        }
    }

    private void DisableAllScriptsOnCamera(Camera cam)
    {
        if (cam == null) return;

        // Disable everything on the camera except the Camera component itself.
        // This kills PlayerView so it can't rotate the corpse anymore.
        var behaviours = cam.GetComponents<MonoBehaviour>();
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            // Don't disable this Death script if it happens to be on camera (unlikely)
            if (b == this) continue;
            b.enabled = false;
        }

        // Also disable scripts on camera children (if you have camera pivot scripts)
        foreach (Transform child in cam.transform)
        {
            if (child == null) continue;
            var childBehaviours = child.GetComponents<MonoBehaviour>();
            foreach (var b in childBehaviours)
            {
                if (b == null) continue;
                b.enabled = false;
            }
        }
    }

    // --- POST PROCESSING LAYER MASK SWITCH ---
    private void SwitchCameraPostProcessingLayer(Camera cam)
    {
        int toLayer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);
        if (toLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorPostProcessingLayerName}' not found. Create it.");
            return;
        }

        int spectatorMask = (1 << toLayer);

        // 1) URP Additional Camera Data (volumeLayerMask)
        // Use Type.GetType with assembly name (string GetComponent often fails).
        System.Type urpType =
            System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

        if (urpType != null)
        {
            var urp = cam.GetComponent(urpType);
            if (urp != null)
            {
                var prop = urpType.GetProperty("volumeLayerMask");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    prop.SetValue(urp, (LayerMask)spectatorMask);
                    Log($"URP volumeLayerMask -> '{spectatorPostProcessingLayerName}'");
                    return;
                }
            }
        }

        // 2) PostProcessing v2 (PostProcessLayer.volumeLayer)
        System.Type ppv2Type =
            System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");

        if (ppv2Type != null)
        {
            var pp = cam.GetComponent(ppv2Type);
            if (pp != null)
            {
                var prop = ppv2Type.GetProperty("volumeLayer");
                if (prop != null && prop.PropertyType == typeof(LayerMask))
                {
                    prop.SetValue(pp, (LayerMask)spectatorMask);
                    Log($"PPv2 volumeLayer -> '{spectatorPostProcessingLayerName}'");
                    return;
                }

                var field = ppv2Type.GetField("volumeLayer");
                if (field != null && field.FieldType == typeof(LayerMask))
                {
                    field.SetValue(pp, (LayerMask)spectatorMask);
                    Log($"PPv2 volumeLayer(field) -> '{spectatorPostProcessingLayerName}'");
                    return;
                }
            }
        }

        Log("Post processing switch FAILED: no URP AdditionalCameraData or PPv2 PostProcessLayer found on the camera.");
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[Death] " + msg, this);
    }
}

/// <summary>
/// Spectator movement controller.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    [HideInInspector] public CharacterController cc;
    [HideInInspector] public Transform cameraTransform;

    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.75f;
    public float lookSensitivity = 2.2f;
    public bool lockCursor = true;

    public bool keepWorldY = true;
    public float fixedWorldY;

    private float yaw;
    private float pitch;

    private void Start()
    {
        if (cameraTransform != null)
        {
            Vector3 e = cameraTransform.rotation.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }

        if (lockCursor)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (cc == null || cameraTransform == null) return;

        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        input = Vector3.ClampMagnitude(input, 1f);

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 move = (transform.right * input.x + transform.forward * input.z) * speed;

        cc.Move(move * Time.deltaTime);

        if (keepWorldY)
        {
            Vector3 p = cameraTransform.position;
            cameraTransform.position = new Vector3(p.x, fixedWorldY, p.z);
        }
    }
}
