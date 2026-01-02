using UnityEngine;
using System.Collections;
using System.Reflection;

/// <summary>
/// Full working Death system (Option B: re-acquire Animator at moment of death)
/// - Call BeginDeath() when sanity hits 0.
/// - Plays death animation (Die) + sound.
/// - Waits spectatorDelay (3s).
/// - Freezes animation on last frame (body stays down).
/// - Switches layers Player -> Spectator (recursively) on BOTH root + cylinder.
/// - Switches camera post-processing layer mask PostProcessing -> SpectatorPostProcessing.
/// - Disables ALL scripts on the Cylinder (every MonoBehaviour).
/// - Creates a spectator controller that moves the Main Camera with collisions and keeps camera at same world Y.
/// </summary>
public class Death : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your PLAYER CYLINDER here (the object that has ALL the player scripts). If empty, uses transform.root.")]
    public GameObject playerCylinder;

    [Tooltip("Animator that contains the Die parameter (usually on the spawned model child). If empty, auto-finds at death time.")]
    public Animator animator;

    public AudioSource audioSource;

    [Tooltip("Main camera to control in spectator. If empty, Camera.main is used.")]
    public Camera mainCamera;

    [Header("Death Animator Parameter")]
    public string dieParamName = "Die";

    public enum DieParamMode { Trigger, Bool }
    [Tooltip("Set this to match your Animator parameter type.")]
    public DieParamMode dieParamMode = DieParamMode.Trigger;

    [Tooltip("If DieParamMode is Bool, this is the value set on death.")]
    public bool dieBoolValue = true;

    [Header("Death Animation")]
    [Tooltip("Enable root motion during death if your death clip uses root motion to drop the body.")]
    public bool enableRootMotionDuringDeath = true;

    [Tooltip("Optional: exact death state name to freeze at the end. Leave blank if unsure.")]
    public string dieStateName = "";

    public int animatorLayer = 0;

    [Header("Audio")]
    public AudioClip deathClip;

    [Header("Timing")]
    public float spectatorDelay = 3f;

    [Header("Layers")]
    public string playerLayerName = "Player";
    public string spectatorLayerName = "Spectator";

    [Header("Post Processing Layer Switch")]
    public string postProcessingLayerName = "PostProcessing";
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    [Header("Disable scripts")]
    [Tooltip("Disable every MonoBehaviour on the playerCylinder (except this Death script until the end).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Disable Death too at the very end (after spectator is created).")]
    public bool disableDeathScriptToo = true;

    [Header("Spectator")]
    public bool spawnSpectatorController = true;

    [Tooltip("Keep camera at EXACT same world Y during spectator (prevents dropping).")]
    public bool keepCameraWorldY = true;

    [Header("Spectator Collision (CharacterController)")]
    public float spectatorControllerHeight = 1.8f;
    public float spectatorControllerRadius = 0.35f;

    [Header("Spectator Movement")]
    public float spectatorMoveSpeed = 5f;
    public float spectatorSprintMultiplier = 1.75f;
    public float spectatorLookSensitivity = 2.2f;
    public bool lockCursor = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deadStarted;

    private void Awake()
    {
        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    /// <summary>Call this once when sanity reaches 0.</summary>
    public void BeginDeath()
    {
        if (deadStarted) return;
        deadStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Log("Death started.");

        // OPTION B: Re-acquire Animator at the moment of death (handles spawners / late-attached visuals)
        EnsureRuntimeReferences();

        if (animator == null)
        {
            Log("ERROR: Animator still not found at death time. This means the model with Animator is NOT under this player's root hierarchy.");
            yield break;
        }

        // Make sure animator is enabled and usable
        animator.enabled = true;
        animator.applyRootMotion = enableRootMotionDuringDeath;
        animator.speed = 1f;

        if (animator.runtimeAnimatorController == null)
        {
            Log("ERROR: Animator has no RuntimeAnimatorController assigned at runtime.");
            yield break;
        }

        // Trigger death animation
        if (dieParamMode == DieParamMode.Trigger)
        {
            animator.ResetTrigger(dieParamName);
            animator.SetTrigger(dieParamName);
            Log($"Animator Trigger set: {dieParamName}");
        }
        else
        {
            animator.SetBool(dieParamName, dieBoolValue);
            Log($"Animator Bool set: {dieParamName} = {dieBoolValue}");
        }

        // Play death sound
        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip);

        // Wait before spectator mode
        yield return new WaitForSeconds(spectatorDelay);

        // Freeze on last frame so the body stays down
        FreezeAnimatorOnLastFrame();

        // Switch layer Player -> Spectator (HARD FORCE on cylinder + root)
        ForceCylinderAndRootToSpectatorLayer();

        // Switch camera post-processing to spectator layer
        SwitchCameraPostProcessingLayer();

        // Disable ALL scripts on Cylinder
        DisableAllScriptsOnCylinder();

        // Create spectator controller and attach camera
        if (spawnSpectatorController)
            CreateSpectatorController();

        // Disable Death script last if desired
        if (disableDeathScriptToo)
            this.enabled = false;

        Log("Death routine complete. Spectator active.");
    }

    private void EnsureRuntimeReferences()
    {
        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Re-find animator under root right now
        if (animator == null)
        {
            animator = transform.root.GetComponentInChildren<Animator>(true);
            Log("Animator re-acquire attempt: " + (animator != null ? "FOUND" : "NOT FOUND"));
        }
    }

    private void FreezeAnimatorOnLastFrame()
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(dieStateName))
        {
            animator.Play(dieStateName, animatorLayer, 1f);
            animator.Update(0f);
        }
        else
        {
            // freeze current state at the end
            var st = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            animator.Play(st.fullPathHash, animatorLayer, 1f);
            animator.Update(0f);
        }

        animator.speed = 0f;
        Log("Animator frozen on last frame.");
    }

    private void ForceCylinderAndRootToSpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorLayerName}' not found. Create it in Unity Layers.");
            return;
        }

        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        // Force both to spectator so nothing stays on Player layer
        SetLayerRecursively(playerCylinder, specLayer);
        SetLayerRecursively(transform.root.gameObject, specLayer);

        Log($"Forced Cylinder + Root layers to '{spectatorLayerName}'.");
    }

    private void DisableAllScriptsOnCylinder()
    {
        if (!disableAllScriptsOnCylinder) return;

        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        MonoBehaviour[] all = playerCylinder.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb == this) continue; // keep Death alive until spectator exists
            mb.enabled = false;
        }

        Log("Disabled ALL MonoBehaviours on Cylinder (except Death temporarily).");
    }

    private void CreateSpectatorController()
    {
        if (mainCamera == null)
        {
            Log("ERROR: No mainCamera found. Assign it or tag camera MainCamera.");
            return;
        }

        Transform camT = mainCamera.transform;
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camWorldPos;
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
            SetLayerRecursively(spec, specLayer);

        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        // Parent camera while preserving world transform
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

        Log("Spectator controller created.");
    }

    private void SwitchCameraPostProcessingLayer()
    {
        if (mainCamera == null) return;

        int toLayer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);
        if (toLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorPostProcessingLayerName}' not found. Create it.");
            return;
        }

        int spectatorMask = (1 << toLayer);

        // URP/HDRP Volume system: UniversalAdditionalCameraData.volumeLayerMask
        var additionalData = mainCamera.GetComponent("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
        if (additionalData != null)
        {
            PropertyInfo p = additionalData.GetType().GetProperty("volumeLayerMask");
            if (p != null && p.PropertyType == typeof(LayerMask))
            {
                p.SetValue(additionalData, (LayerMask)spectatorMask);
                Log($"URP Volume Layer Mask switched to '{spectatorPostProcessingLayerName}'.");
                return;
            }
        }

        // PostProcessing v2: PostProcessLayer.volumeLayer
        var ppl = mainCamera.GetComponent("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
        if (ppl != null)
        {
            var t = ppl.GetType();

            PropertyInfo prop = t.GetProperty("volumeLayer");
            if (prop != null && prop.PropertyType == typeof(LayerMask))
            {
                prop.SetValue(ppl, (LayerMask)spectatorMask);
                Log($"PPv2 volumeLayer switched to '{spectatorPostProcessingLayerName}'.");
                return;
            }

            FieldInfo field = t.GetField("volumeLayer");
            if (field != null && field.FieldType == typeof(LayerMask))
            {
                field.SetValue(ppl, (LayerMask)spectatorMask);
                Log($"PPv2 volumeLayer switched to '{spectatorPostProcessingLayerName}'.");
                return;
            }
        }

        Log("No post-processing component found to switch (URP AdditionalCameraData or PPv2 PostProcessLayer).");
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

    private void Log(string msg)
    {
        if (debugLogs)
            Debug.Log("[Death] " + msg, this);
    }
}

/// <summary>
/// Moves camera in spectator mode with collisions (CharacterController).
/// Keeps camera at fixed world Y if enabled.
/// Uses UnityEngine.Cursor explicitly to avoid conflicts.
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
