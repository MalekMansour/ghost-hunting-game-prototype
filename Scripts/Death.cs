using UnityEngine;
using System.Collections;

public class Death : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Your PLAYER CYLINDER (scripts live here). If empty, uses transform.root.")]
    public GameObject playerCylinder;

    [Tooltip("Animator on model. Leave empty: auto-find at death time.")]
    public Animator animator;

    [Tooltip("Model root where ragdoll bones exist. Leave empty: uses animator.transform.")]
    public Transform modelRoot;

    [Tooltip("Optional: hips/pelvis bone (best for rotation + ground snapping). Drag in Inspector.")]
    public Transform ragdollHips;

    public AudioSource audioSource;
    public Camera mainCamera;

    [Header("Death Animator Parameter")]
    public string dieParamName = "Die";
    public enum DieParamMode { Trigger, Bool }
    public DieParamMode dieParamMode = DieParamMode.Trigger;
    public bool dieBoolValue = true;

    [Header("Timing")]
    public float spectatorDelay = 3f;
    public float ragdollDelay = 0.75f;

    [Header("Audio")]
    public AudioClip deathClip;

    [Header("Layers")]
    public string spectatorLayerName = "Spectator";

    [Header("Ragdoll")]
    public bool detachModelOnRagdoll = true;
    public bool disableCylinderCharacterControllerOnRagdoll = true;

    public enum CorpseRotationMode { None, UseEuler, UseTransform }
    [Header("Corpse Rotation")]
    public CorpseRotationMode corpseRotationMode = CorpseRotationMode.None;
    public Vector3 corpseEulerRotation = new Vector3(90f, 0f, 0f);
    public Transform corpseRotationReference;

    [Tooltip("Prevents physics from standing the hips back upright (optional).")]
    public bool lockHipsRotationXZ = true;

    public enum CorpsePositionMode { None, SnapToGround, SnapToGroundAndOffset }
    [Header("Corpse Position (Fix floating)")]
    public CorpsePositionMode corpsePositionMode = CorpsePositionMode.SnapToGround;

    [Tooltip("What counts as ground (Default/Environment/etc).")]
    public LayerMask corpseGroundMask = ~0;

    [Tooltip("Max distance to snap down to ground.")]
    public float snapDownMaxDistance = 3f;

    [Tooltip("Extra offset applied after snapping (use small values like Y = 0.02).")]
    public Vector3 corpseLocalOffset = new Vector3(0f, 0.02f, 0f);

    [Header("Disable scripts")]
    public bool disableAllScriptsOnCylinder = true;

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

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;

    private Collider[] cylinderColliders;
    private CharacterController cylinderCC;

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

        // trigger death animation
        animator.enabled = true;
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

        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip);

        if (ragdollDelay > 0f)
            yield return new WaitForSeconds(ragdollDelay);

        EnableRagdoll();

        // rotate corpse (optional) then snap it to floor (fix floating)
        ApplyCorpseRotation();
        yield return null; // one frame so physics updates
        SnapCorpseToGround();

        float remaining = Mathf.Max(0f, spectatorDelay - ragdollDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        ForceAllToSpectatorLayer();
        EnterSpectator();
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

            SetRagdollState(false);
        }
    }

    private void EnableRagdoll()
    {
        if (modelRoot == null || ragdollBodies == null || ragdollBodies.Length == 0)
        {
            Log("Ragdoll FAILED: no rigidbodies under modelRoot. (Need ragdoll rig.)");
            return;
        }

        if (detachModelOnRagdoll && modelRoot.parent != null)
            modelRoot.SetParent(null, true);

        // stop animator so it doesn't fight ragdoll
        animator.enabled = false;

        if (disableCylinderCharacterControllerOnRagdoll && cylinderCC != null)
            cylinderCC.enabled = false;

        SetRagdollState(true);

        // disable cylinder colliders that aren't part of model
        if (cylinderColliders != null && modelRoot != null)
        {
            foreach (var c in cylinderColliders)
            {
                if (c == null) continue;
                if (c.transform.IsChildOf(modelRoot)) continue;
                c.enabled = false;
            }
        }
    }

    private void SetRagdollState(bool enabled)
    {
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = enabled;
        }
    }

    private void ApplyCorpseRotation()
    {
        if (corpseRotationMode == CorpseRotationMode.None) return;

        Transform target = ragdollHips != null ? ragdollHips : modelRoot;
        if (target == null) return;

        if (corpseRotationMode == CorpseRotationMode.UseEuler)
        {
            target.rotation = Quaternion.Euler(corpseEulerRotation);
        }
        else if (corpseRotationMode == CorpseRotationMode.UseTransform && corpseRotationReference != null)
        {
            target.rotation = corpseRotationReference.rotation;
        }

        if (lockHipsRotationXZ && ragdollHips != null)
        {
            var hipsRb = ragdollHips.GetComponent<Rigidbody>();
            if (hipsRb != null)
            {
                hipsRb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }

    private void SnapCorpseToGround()
    {
        if (corpsePositionMode == CorpsePositionMode.None) return;

        // We snap the entire modelRoot down based on a raycast from the hips (or model root)
        if (modelRoot == null) return;

        Transform probe = ragdollHips != null ? ragdollHips : modelRoot;

        Vector3 origin = probe.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, snapDownMaxDistance + 0.5f, corpseGroundMask, QueryTriggerInteraction.Ignore))
        {
            // Move modelRoot down so the probe sits on the hit point (plus small offset)
            float deltaY = probe.position.y - hit.point.y;

            // push model down
            modelRoot.position -= new Vector3(0f, deltaY, 0f);

            if (corpsePositionMode == CorpsePositionMode.SnapToGroundAndOffset)
                modelRoot.position += corpseLocalOffset;

            // also zero out velocities a bit so it settles
            foreach (var rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.velocity *= 0.1f;
                rb.angularVelocity *= 0.1f;
            }
        }
    }

    private void ForceAllToSpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"ERROR: Layer '{spectatorLayerName}' not found.");
            return;
        }

        SetLayerRecursively(transform.root.gameObject, specLayer);
        if (playerCylinder != null) SetLayerRecursively(playerCylinder, specLayer);
        if (modelRoot != null) SetLayerRecursively(modelRoot.gameObject, specLayer);
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

    private void EnterSpectator()
    {
        if (mainCamera == null) return;

        // camera should SEE the corpse on Spectator layer
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
            mainCamera.cullingMask |= (1 << specLayer);

        // tell PlayerView to switch PP + stop rotating body
        var pv = mainCamera.GetComponent<PlayerView>();
        if (pv != null)
            pv.SetSpectatorMode(true);

        // disable all scripts on cylinder (movement etc.)
        if (disableAllScriptsOnCylinder && playerCylinder != null)
        {
            var all = playerCylinder.GetComponents<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                if (mb == this) continue;
                mb.enabled = false;
            }
        }

        // detach camera to spectator controller
        Transform camT = mainCamera.transform;
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camWorldPos;
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        if (specLayer >= 0) SetLayerRecursively(spec, specLayer);

        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        camT.SetParent(spec.transform, true);
        camT.position = camWorldPos;
        camT.rotation = camWorldRot;

        var controller = spec.AddComponent<SpectatorController>();
        controller.cc = cc;
        controller.cameraTransform = camT;
        controller.moveSpeed = spectatorMoveSpeed;
        controller.sprintMultiplier = spectatorSprintMultiplier;
        controller.lookSensitivity = spectatorLookSensitivity;
        controller.lockCursor = lockCursor;
        controller.keepWorldY = keepCameraWorldY;
        controller.fixedWorldY = camWorldPos.y;
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
