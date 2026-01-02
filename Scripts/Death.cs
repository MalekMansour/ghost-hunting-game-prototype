using UnityEngine;
using System.Collections;
using System.Reflection;

public class Death : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your PLAYER CYLINDER here (the object that has ALL the player scripts). If empty, uses transform.root.")]
    public GameObject playerCylinder;

    [Tooltip("Animator that contains the Die parameter (usually on the model child). If empty, auto-finds in children.")]
    public Animator animator;

    public AudioSource audioSource;

    [Tooltip("Main camera to control in spectator. If empty, Camera.main is used.")]
    public Camera mainCamera;

    [Header("Death Parameter")]
    public string dieParamName = "Die";

    public enum DieParamMode { Trigger, Bool }
    [Tooltip("IMPORTANT: If your Animator uses a BOOL named 'Die' (very common), set this to Bool.")]
    public DieParamMode dieParamMode = DieParamMode.Trigger;

    [Tooltip("If DieParamMode is Bool, set this value on death.")]
    public bool dieBoolValue = true;

    [Header("Root Motion / Falling to floor")]
    [Tooltip("Enable root motion during death if your death animation moves the body down.")]
    public bool enableRootMotionDuringDeath = true;

    [Tooltip("Optional: exact state name for the death anim to freeze precisely at the end. Leave blank if unsure.")]
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
    [Tooltip("Camera post-processing will switch from this Layer to the spectator Layer when spectator begins.")]
    public string postProcessingLayerName = "PostProcessing";
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    [Header("Disable scripts")]
    [Tooltip("Disable every MonoBehaviour on the playerCylinder (except this Death script until the end).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Disable Death too at the very end (after spectator is created).")]
    public bool disableDeathScriptToo = true;

    [Header("Spectator")]
    public bool spawnSpectatorController = true;
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
    public bool debugLogs = false;

    private bool deadStarted;

    private void Awake()
    {
        // Cylinder default: root (safer than gameObject)
        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Auto-find animator in children (usually on model)
        if (animator == null)
            animator = playerCylinder.GetComponentInChildren<Animator>(true);
    }

    public void BeginDeath()
    {
        if (deadStarted) return;
        deadStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Log("Death started.");

        // Make absolutely sure animator is valid and enabled
        if (animator == null)
        {
            Log("ERROR: Animator not found. Assign animator on the model or ensure it exists in children.");
        }
        else
        {
            animator.enabled = true;

            if (animator.runtimeAnimatorController == null)
            {
                Log("ERROR: Animator has no Controller assigned at runtime.");
            }

            animator.applyRootMotion = enableRootMotionDuringDeath;
            animator.speed = 1f;

            // Set death parameter (Trigger OR Bool)
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
        }

        // Death audio
        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip);

        // Wait before spectator
        yield return new WaitForSeconds(spectatorDelay);

        // Freeze pose so body stays on floor (if animation actually moved it)
        FreezeAnimatorOnLastFrame();

        // HARD FORCE: set Cylinder + everything under it to Spectator layer
        ForceCylinderToSpectatorLayer();

        // Switch camera post-processing layer mask
        SwitchCameraPostProcessingLayer();

        // Disable ALL scripts on cylinder (everything)
        DisableAllScriptsOnCylinder();

        // Spawn spectator controller + attach camera
        if (spawnSpectatorController)
            CreateSpectatorController();

        // Disable this script last if you want
        if (disableDeathScriptToo)
            this.enabled = false;

        Log("Death routine complete.");
    }

    private void FreezeAnimatorOnLastFrame()
    {
        if (animator == null) return;

        // If you provided a state name, jump to its end.
        if (!string.IsNullOrEmpty(dieStateName))
        {
            animator.Play(dieStateName, animatorLayer, 1f);
            animator.Update(0f);
        }
        else
        {
            // Freeze whatever state is currently active on that layer
            var st = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            animator.Play(st.fullPathHash, animatorLayer, 1f);
            animator.Update(0f);
        }

        animator.speed = 0f;
        Log("Animator frozen on last frame.");
    }

    private void ForceCylinderToSpectatorLayer()
    {
        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorLayerName}' does not exist. Create it in Unity Layers.");
            return;
        }

        SetLayerRecursively(playerCylinder, specLayer);

        // OPTIONAL extra safety: if your model is NOT under cylinder, also set root
        SetLayerRecursively(transform.root.gameObject, specLayer);

        Log($"Forced Cylinder + Root to layer: {spectatorLayerName}");
    }

    private void DisableAllScriptsOnCylinder()
    {
        if (!disableAllScriptsOnCylinder) return;

        if (playerCylinder == null)
            playerCylinder = transform.root.gameObject;

        var all = playerCylinder.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;

            // Keep Death alive until spectator is spawned
            if (mb == this) continue;

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

        int fromLayer = LayerMask.NameToLayer(postProcessingLayerName);
        int toLayer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);

        if (toLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorPostProcessingLayerName}' does not exist. Create it.");
            return;
        }

        // We will set volumeLayerMask to ONLY the spectator PP layer.
        int spectatorMask = (1 << toLayer);

        // -------- URP/HDRP Volume Mask (UniversalAdditionalCameraData.volumeLayerMask) ----------
        // Type: UnityEngine.Rendering.Universal.UniversalAdditionalCameraData
        // Property: volumeLayerMask
        var cam = mainCamera;
        var additionalData = cam.GetComponent("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
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

        // -------- PostProcessing v2 (PostProcessLayer.volumeLayer) ----------
        // Type: UnityEngine.Rendering.PostProcessing.PostProcessLayer
        // Field/Property: volumeLayer
        var ppl = cam.GetComponent("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
        if (ppl != null)
        {
            var t = ppl.GetType();

            // Try property first
            PropertyInfo prop = t.GetProperty("volumeLayer");
            if (prop != null && prop.PropertyType == typeof(LayerMask))
            {
                prop.SetValue(ppl, (LayerMask)spectatorMask);
                Log($"PPv2 volumeLayer switched to '{spectatorPostProcessingLayerName}'.");
                return;
            }

            // Try field fallback
            FieldInfo field = t.GetField("volumeLayer");
            if (field != null && field.FieldType == typeof(LayerMask))
            {
                field.SetValue(ppl, (LayerMask)spectatorMask);
                Log($"PPv2 volumeLayer switched to '{spectatorPostProcessingLayerName}'.");
                return;
            }
        }

        Log("No known post-processing component found to switch layer mask (URP AdditionalCameraData / PPv2 PostProcessLayer).");
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
