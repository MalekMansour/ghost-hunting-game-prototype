using UnityEngine;
using System.Collections;

public class Death : MonoBehaviour
{
    [Header("References")]
    public GameObject playerCylinder;
    public Animator animator;
    public Transform modelRoot;
    public Transform ragdollHips;
    public Camera mainCamera;
    public AudioSource audioSource;

    [Header("Animator")]
    public string dieParamName = "Die";
    public enum DieParamMode { Trigger, Bool }
    public DieParamMode dieParamMode = DieParamMode.Trigger;
    public bool dieBoolValue = true;

    [Header("Timing")]
    [Tooltip("Spectator starts after this many seconds (animation/sound still start instantly).")]
    public float spectatorDelay = 3f;

    [Tooltip("0 = ragdoll instantly. Increase if you want the animation to play first.")]
    public float ragdollDelay = 0f;

    [Header("Death Sound")]
    public AudioClip deathClip;
    [Range(0f, 1f)]
    public float deathSoundVolume = 0.7f;

    [Header("Layers")]
    public string spectatorLayerName = "Spectator";

    [Header("Ragdoll")]
    public bool detachModelOnRagdoll = true;
    public bool disableCylinderCharacterControllerOnRagdoll = true;

    public enum CorpseRotationMode { None, UseEuler }
    [Header("Corpse Base Rotation (Inspector)")]
    public CorpseRotationMode corpseRotationMode = CorpseRotationMode.None;
    public Vector3 corpseEulerRotation = new Vector3(90f, 0f, 0f);

    public enum CorpsePositionMode { None, GroundUsingLowestColliderAndOffset }
    [Header("Corpse Grounding (FIX floating)")]
    public CorpsePositionMode corpsePositionMode = CorpsePositionMode.GroundUsingLowestColliderAndOffset;

    [Tooltip("What counts as ground.")]
    public LayerMask corpseGroundMask = ~0;

    [Tooltip("How far down we try to find ground.")]
    public float snapDownMaxDistance = 10f;

    [Tooltip("Offset applied AFTER grounding.")]
    public Vector3 corpseOffsetAfterSnap = new Vector3(0f, 0.02f, 0f);

    [Header("Disable scripts")]
    public bool disableAllScriptsOnCylinder = true;

    [Header("Spectator")]
    public float spectatorControllerHeight = 1.8f;
    public float spectatorControllerRadius = 0.35f;
    public float spectatorMoveSpeed = 5f;
    public float spectatorSprintMultiplier = 1.75f;
    public float spectatorLookSensitivity = 2.2f;
    public bool lockCursor = true;
    public bool keepCameraWorldY = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deadStarted = false;
    private bool soundPlayed = false;

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

        // Animation starts immediately (no delay)
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

        // Sound starts immediately — and ONLY ONCE
        PlayDeathSoundOnce();

        // Optional ragdoll delay (default 0)
        if (ragdollDelay > 0f)
            yield return new WaitForSeconds(ragdollDelay);

        EnableRagdoll();

        // Base rotation first (optional)
        ApplyBaseCorpseRotation();

        yield return null; // one frame so ragdoll colliders/bounds update

        // Proper grounding using the lowest collider point
        GroundCorpseNow();

        // spectator after spectatorDelay
        if (spectatorDelay > 0f)
            yield return new WaitForSeconds(spectatorDelay);

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

    private void PlayDeathSoundOnce()
    {
        if (soundPlayed) return;
        soundPlayed = true;

        if (audioSource != null && deathClip != null)
        {
            audioSource.PlayOneShot(deathClip, deathSoundVolume);
        }
    }

    private void EnableRagdoll()
    {
        if (modelRoot == null || ragdollBodies == null || ragdollBodies.Length == 0)
        {
            Log("Ragdoll FAILED: no rigidbodies under modelRoot (need ragdoll rig).");
            return;
        }

        if (detachModelOnRagdoll && modelRoot.parent != null)
            modelRoot.SetParent(null, true);

        // Stop animator so it doesn't fight ragdoll
        if (animator != null) animator.enabled = false;

        if (disableCylinderCharacterControllerOnRagdoll && cylinderCC != null)
            cylinderCC.enabled = false;

        SetRagdollState(true);

        // Disable cylinder colliders that aren't part of the model
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

    private void ApplyBaseCorpseRotation()
    {
        if (corpseRotationMode == CorpseRotationMode.None) return;

        Transform target = ragdollHips != null ? ragdollHips : modelRoot;
        if (target == null) return;

        target.rotation = Quaternion.Euler(corpseEulerRotation);
    }

    /// <summary>
    /// Grounds the corpse by finding the LOWEST ragdoll collider point (bounds.min.y),
    /// then moving modelRoot down so that lowest point sits on the ground hit point,
    /// then applies corpseOffsetAfterSnap.
    /// </summary>
    private void GroundCorpseNow()
    {
        if (corpsePositionMode == CorpsePositionMode.None) return;
        if (modelRoot == null || ragdollColliders == null || ragdollColliders.Length == 0) return;

        // 1) Find lowest ragdoll collider Y (world space)
        float lowestY = float.PositiveInfinity;
        foreach (var col in ragdollColliders)
        {
            if (col == null || !col.enabled) continue;
            float y = col.bounds.min.y;
            if (y < lowestY) lowestY = y;
        }
        if (float.IsInfinity(lowestY)) return;

        // 2) Raycast down from above that point to find ground
        Vector3 origin = new Vector3(modelRoot.position.x, lowestY + 1.0f, modelRoot.position.z);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, snapDownMaxDistance + 1.0f, corpseGroundMask, QueryTriggerInteraction.Ignore))
        {
            // If the center ray misses, try from hips (some maps have uneven ground)
            Transform probe = ragdollHips != null ? ragdollHips : modelRoot;
            Vector3 origin2 = probe.position + Vector3.up * 1.0f;

            if (!Physics.Raycast(origin2, Vector3.down, out hit, snapDownMaxDistance + 2.0f, corpseGroundMask, QueryTriggerInteraction.Ignore))
                return;
        }

        // 3) Recompute lowestY after any physics nudge (safe enough to reuse previous, but we re-evaluate)
        lowestY = float.PositiveInfinity;
        foreach (var col in ragdollColliders)
        {
            if (col == null || !col.enabled) continue;
            float y = col.bounds.min.y;
            if (y < lowestY) lowestY = y;
        }
        if (float.IsInfinity(lowestY)) return;

        // 4) Move modelRoot so lowest collider touches the ground hit point
        float delta = lowestY - hit.point.y;
        modelRoot.position -= new Vector3(0f, delta, 0f);

        // 5) Apply your tuned offset
        modelRoot.position += corpseOffsetAfterSnap;
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

        // Make sure camera can see spectator layer (corpse)
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
            mainCamera.cullingMask |= (1 << specLayer);

        // Switch PP + stop body rotation
        var pv = mainCamera.GetComponent<PlayerView>();
        if (pv != null)
            pv.SetSpectatorMode(true);

        // Disable all scripts on cylinder (movement, interaction, etc.)
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

        // Detach camera and create spectator controller
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
