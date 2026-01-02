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

    [Tooltip("Root transform of the model (where ragdoll bones live). Leave empty: will use animator.transform.")]
    public Transform modelRoot;

    public AudioSource audioSource;

    [Tooltip("Main camera (child of player at runtime). Leave empty: Camera.main.")]
    public Camera mainCamera;

    [Header("Death Animator Parameter")]
    public string dieParamName = "Die";
    public enum DieParamMode { Trigger, Bool }
    public DieParamMode dieParamMode = DieParamMode.Trigger;
    public bool dieBoolValue = true;

    [Header("Death Timing")]
    public float spectatorDelay = 3f;
    public float ragdollDelay = 0.75f;

    [Tooltip("Enable root motion during the short death animation start (if your clip uses it).")]
    public bool enableRootMotionDuringDeath = true;

    [Header("Audio")]
    public AudioClip deathClip;

    [Header("Layers")]
    public string spectatorLayerName = "Spectator";
    public string spectatorPostProcessingLayerName = "SpectatorPostProcessing";

    [Header("Disable scripts")]
    public bool disableAllScriptsOnCylinder = true;
    public bool disableAllCameraScripts = true;
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

    [Header("Ragdoll")]
    [Tooltip("Detach the model from the player cylinder when ragdoll starts (fixes 'standing corpse').")]
    public bool detachModelOnRagdoll = true;

    [Tooltip("If true, disables the CharacterController on the cylinder when ragdoll starts (prevents capsule from holding body).")]
    public bool disableCylinderCharacterControllerOnRagdoll = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deadStarted;

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;

    private Collider[] cylinderColliders;
    private CharacterController cylinderCC;

    private Transform originalModelParent;

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
            Log("ERROR: Animator not found at death time.");
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

        // Wait, then ragdoll
        if (ragdollDelay > 0f)
            yield return new WaitForSeconds(ragdollDelay);

        EnableRagdoll();

        // Wait remaining time until spectator
        float remaining = Mathf.Max(0f, spectatorDelay - ragdollDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // Switch player layers to Spectator (you requested this)
        ForceRootAndCylinderToSpectatorLayer();

        // Enter spectator (camera DETACHED + includes spectator layer so corpse is visible)
        EnterSpectator();

        if (disableDeathScriptToo)
            this.enabled = false;
    }

    private void EnsureRuntimeReferences()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;
        if (mainCamera == null) mainCamera = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (animator == null)
            animator = transform.root.GetComponentInChildren<Animator>(true);

        if (modelRoot == null && animator != null)
            modelRoot = animator.transform;

        if (modelRoot != null && ragdollBodies == null)
        {
            ragdollBodies = modelRoot.GetComponentsInChildren<Rigidbody>(true);
            ragdollColliders = modelRoot.GetComponentsInChildren<Collider>(true);

            cylinderColliders = playerCylinder.GetComponentsInChildren<Collider>(true);
            cylinderCC = playerCylinder.GetComponent<CharacterController>();

            // Start ragdoll OFF
            SetRagdollState(false);
        }
    }

    // ---------- RAGDOLL ----------
    private void EnableRagdoll()
    {
        if (modelRoot == null || ragdollBodies == null || ragdollBodies.Length == 0)
        {
            Log("Ragdoll FAILED: no rigidbodies found under modelRoot. You need a ragdoll rig (Rigidbody + Collider on bones).");
            return;
        }

        // Detach model so the cylinder/root doesn't keep it upright
        if (detachModelOnRagdoll && modelRoot.parent != null)
        {
            originalModelParent = modelRoot.parent;
            Vector3 p = modelRoot.position;
            Quaternion r = modelRoot.rotation;

            modelRoot.SetParent(null, true); // keep world transform
            modelRoot.position = p;
            modelRoot.rotation = r;

            Log("Model detached from player root for ragdoll.");
        }

        // Stop animator (don’t fight physics)
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = false;
        }

        // Disable cylinder CC so it can't hold/rotate anything
        if (disableCylinderCharacterControllerOnRagdoll && cylinderCC != null)
            cylinderCC.enabled = false;

        // Enable ragdoll physics
        SetRagdollState(true);

        // Disable cylinder colliders that are NOT part of the model (prevents capsule/cylinder from blocking corpse)
        if (cylinderColliders != null && modelRoot != null)
        {
            foreach (var c in cylinderColliders)
            {
                if (c == null) continue;

                // Keep model/bone colliders ON
                if (c.transform.IsChildOf(modelRoot)) continue;

                c.enabled = false;
            }
        }

        Log("Ragdoll ENABLED.");
    }

    private void SetRagdollState(bool enabled)
    {
        // Rigidbody on bones
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;
        }

        // Colliders on bones
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = enabled;
        }
    }

    // ---------- LAYERS ----------
    private void ForceRootAndCylinderToSpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorLayerName}' not found. Create it in Unity Layers.");
            return;
        }

        SetLayerRecursively(transform.root.gameObject, specLayer);
        if (playerCylinder != null) SetLayerRecursively(playerCylinder, specLayer);

        // If modelRoot was detached, also set it to spectator so it matches your requirement
        if (modelRoot != null) SetLayerRecursively(modelRoot.gameObject, specLayer);

        Log($"Forced Root + Cylinder (+ detached model) to layer '{spectatorLayerName}'.");
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

    // ---------- SPECTATOR ----------
    private void EnterSpectator()
    {
        if (mainCamera == null)
        {
            Log("ERROR: mainCamera missing.");
            return;
        }

        // Make sure camera can SEE Spectator layer (this is why your corpse disappeared)
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
            mainCamera.cullingMask |= (1 << specLayer);

        // Switch post processing layer mask on the camera to SpectatorPostProcessing
        SwitchCameraPostProcessingLayer(mainCamera);

        // Disable all scripts on cylinder (movement etc.)
        if (disableAllScriptsOnCylinder && playerCylinder != null)
        {
            var all = playerCylinder.GetComponents<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                if (mb == this) continue; // keep Death alive until spectator exists
                mb.enabled = false;
            }
        }

        // Disable camera scripts like PlayerView so it can never rotate the body again
        if (disableAllCameraScripts)
        {
            var camBehaviours = mainCamera.GetComponents<MonoBehaviour>();
            foreach (var b in camBehaviours)
            {
                if (b == null) continue;
                if (b == this) continue;
                b.enabled = false;
            }
        }

        // FULL DETACH: camera is no longer under player
        Transform camT = mainCamera.transform;
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camWorldPos;
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

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

        Log("Spectator entered: camera detached + corpse visible.");
    }

    // ---------- POST PROCESSING SWITCH ----------
    private void SwitchCameraPostProcessingLayer(Camera cam)
    {
        int toLayer = LayerMask.NameToLayer(spectatorPostProcessingLayerName);
        if (toLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorPostProcessingLayerName}' not found. Create it.");
            return;
        }

        int spectatorMask = (1 << toLayer);

        // URP: UniversalAdditionalCameraData.volumeLayerMask
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

        // PPv2: PostProcessLayer.volumeLayer
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

        Log("Post processing switch FAILED: camera has neither URP AdditionalCameraData nor PPv2 PostProcessLayer.");
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[Death] " + msg, this);
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
