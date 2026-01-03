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

    public enum CorpsePositionMode { None, SnapToGroundAndOffset }
    [Header("Corpse Base Position (Inspector)")]
    public CorpsePositionMode corpsePositionMode = CorpsePositionMode.SnapToGroundAndOffset;
    public LayerMask corpseGroundMask = ~0;
    public float snapDownMaxDistance = 3f;
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

    [Header("Live Corpse Tuning (PLAY MODE)")]
    public bool enableLiveTuning = true;

    [Tooltip("Hold LeftAlt to use 'fast' tuning speed.")]
    public float tuneMoveStep = 0.01f;
    public float tuneMoveStepFast = 0.05f;

    [Tooltip("Hold LeftAlt to use 'fast' tuning rotation.")]
    public float tuneRotStep = 1f;
    public float tuneRotStepFast = 5f;

    [Tooltip("Press this to print final tuning values to console.")]
    public KeyCode printTuningKey = KeyCode.P;

    [Tooltip("Rotation keys: J/L yaw, I/K pitch, U/O roll.")]
    public bool showKeyHelpOnDeath = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deadStarted = false;
    private bool soundPlayed = false;

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private Collider[] cylinderColliders;
    private CharacterController cylinderCC;

    // runtime tuning deltas (so you can copy them back)
    private Vector3 runtimePosDelta = Vector3.zero;
    private Vector3 runtimeRotDeltaEuler = Vector3.zero;

    private bool spectatorActive = false;

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

        // Sound starts immediately (no delay) — and ONLY ONCE
        PlayDeathSoundOnce();

        // Ragdoll timing (you requested no delay -> default ragdollDelay = 0)
        if (ragdollDelay > 0f)
            yield return new WaitForSeconds(ragdollDelay);

        EnableRagdoll();

        // Apply base rotation/position once (then you can fine-tune live)
        ApplyBaseCorpseRotation();
        yield return null; // one frame
        ApplyBaseCorpseSnap();

        // spectator after spectatorDelay (animation/sound already started instantly)
        if (spectatorDelay > 0f)
            yield return new WaitForSeconds(spectatorDelay);

        ForceAllToSpectatorLayer();
        EnterSpectator();

        spectatorActive = true;

        if (showKeyHelpOnDeath && enableLiveTuning)
        {
            Log("LIVE TUNING KEYS (while spectating):");
            Log("Move:  T/G = +Y/-Y,  F/H = -X/+X,  R/Y = +Z/-Z");
            Log("Rotate: I/K pitch, J/L yaw, U/O roll. Hold LeftAlt for fast.");
            Log("Press P to print the final values you should copy into Inspector.");
        }
    }

    private void Update()
    {
        if (!enableLiveTuning) return;
        if (!spectatorActive) return;

        // Only tune if we have a model root
        if (modelRoot == null) return;

        float moveStep = Input.GetKey(KeyCode.LeftAlt) ? tuneMoveStepFast : tuneMoveStep;
        float rotStep = Input.GetKey(KeyCode.LeftAlt) ? tuneRotStepFast : tuneRotStep;

        bool changed = false;

        // POSITION: (local-ish in world axes for simplicity)
        if (Input.GetKey(KeyCode.T)) { runtimePosDelta += Vector3.up * moveStep; changed = true; }
        if (Input.GetKey(KeyCode.G)) { runtimePosDelta += Vector3.down * moveStep; changed = true; }

        if (Input.GetKey(KeyCode.H)) { runtimePosDelta += Vector3.right * moveStep; changed = true; } // +X
        if (Input.GetKey(KeyCode.F)) { runtimePosDelta += Vector3.left * moveStep; changed = true; }  // -X

        if (Input.GetKey(KeyCode.R)) { runtimePosDelta += Vector3.forward * moveStep; changed = true; } // +Z
        if (Input.GetKey(KeyCode.Y)) { runtimePosDelta += Vector3.back * moveStep; changed = true; }    // -Z

        // ROTATION:
        if (Input.GetKey(KeyCode.I)) { runtimeRotDeltaEuler.x += rotStep; changed = true; } // pitch +
        if (Input.GetKey(KeyCode.K)) { runtimeRotDeltaEuler.x -= rotStep; changed = true; } // pitch -
        if (Input.GetKey(KeyCode.L)) { runtimeRotDeltaEuler.y += rotStep; changed = true; } // yaw +
        if (Input.GetKey(KeyCode.J)) { runtimeRotDeltaEuler.y -= rotStep; changed = true; } // yaw -
        if (Input.GetKey(KeyCode.O)) { runtimeRotDeltaEuler.z += rotStep; changed = true; } // roll +
        if (Input.GetKey(KeyCode.U)) { runtimeRotDeltaEuler.z -= rotStep; changed = true; } // roll -

        if (changed)
        {
            ApplyRuntimeTuning();
        }

        if (Input.GetKeyDown(printTuningKey))
        {
            PrintTuningValues();
        }
    }

    private void ApplyRuntimeTuning()
    {
        // apply to modelRoot (and hips rotation if provided)
        modelRoot.position += runtimePosDelta;
        runtimePosDelta = Vector3.zero;

        if (ragdollHips != null)
        {
            ragdollHips.rotation = Quaternion.Euler(ragdollHips.rotation.eulerAngles + runtimeRotDeltaEuler);
        }
        else
        {
            modelRoot.rotation = Quaternion.Euler(modelRoot.rotation.eulerAngles + runtimeRotDeltaEuler);
        }
        runtimeRotDeltaEuler = Vector3.zero;
    }

    private void PrintTuningValues()
    {
        // Tell you what to copy into inspector:
        // - corpseEulerRotation
        // - corpseOffsetAfterSnap
        Vector3 rot = corpseEulerRotation;
        if (ragdollHips != null)
            rot = ragdollHips.rotation.eulerAngles;

        Vector3 pos = corpseOffsetAfterSnap;

        Log("---- COPY THESE INTO INSPECTOR ----");
        Log($"corpseEulerRotation = new Vector3({rot.x:F2}f, {rot.y:F2}f, {rot.z:F2}f)");
        Log($"corpseOffsetAfterSnap = new Vector3({pos.x:F4}f, {pos.y:F4}f, {pos.z:F4}f)");
        Log("-----------------------------------");
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

    private void ApplyBaseCorpseSnap()
    {
        if (corpsePositionMode == CorpsePositionMode.None) return;
        if (modelRoot == null) return;

        Transform probe = ragdollHips != null ? ragdollHips : modelRoot;

        Vector3 origin = probe.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, snapDownMaxDistance + 0.5f, corpseGroundMask, QueryTriggerInteraction.Ignore))
        {
            float deltaY = probe.position.y - hit.point.y;
            modelRoot.position -= new Vector3(0f, deltaY, 0f);
            modelRoot.position += corpseOffsetAfterSnap;
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
