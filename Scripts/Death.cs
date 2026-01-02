using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Death : MonoBehaviour
{
    [Header("Death Timing")]
    [Tooltip("How long to wait AFTER triggering the death animation before ragdoll starts.")]
    public float ragdollDelay = 3f;

    [Tooltip("How long to wait AFTER ragdoll starts before switching player into spectator mode.")]
    public float spectatorDelayAfterRagdoll = 0.15f;

    [Header("Model / Ragdoll")]
    [Tooltip("Root transform of the PLAYER MODEL (skeleton/mesh). If empty, auto-uses Animator transform.")]
    public Transform modelRoot;

    [Tooltip("Animator on the player model. If empty, auto-find in children.")]
    public Animator playerAnimator;

    [Tooltip("If true, we disable the animator when ragdoll begins.")]
    public bool disableAnimatorOnRagdoll = true;

    [Header("Main Player Collision (living controller)")]
    [Tooltip("Your player cylinder/capsule object (the collider you walk with). If empty, we try auto-find CharacterController root.")]
    public Transform playerColliderRoot;

    [Tooltip("Disable CharacterController on death so it doesn't fight ragdoll.")]
    public bool disableCharacterControllerOnDeath = true;

    [Tooltip("Disable Rigidbody on the main player collider root (if you use one).")]
    public bool disableMainRigidbodyOnDeath = false;

    [Header("Camera")]
    [Tooltip("Main player camera (if empty uses Camera.main).")]
    public Camera mainCamera;

    [Header("Spectator")]
    [Tooltip("Layer name spectator should be set to. Ghosts must NOT include this in their playerLayer mask.")]
    public string spectatorLayerName = "Spectator";

    [Tooltip("When spectator spawns, camera will parent to it.")]
    public bool attachCameraToSpectator = true;

    [Tooltip("Spectator capsule radius/height.")]
    public float spectatorRadius = 0.35f;
    public float spectatorHeight = 1.7f;

    [Tooltip("Movement speed as spectator.")]
    public float spectatorMoveSpeed = 5.5f;

    [Tooltip("Sprint multiplier.")]
    public float spectatorSprintMultiplier = 1.6f;

    [Tooltip("Gravity applied to spectator so it stays on floors and doesn't float.")]
    public float spectatorGravity = 18f;

    [Tooltip("Mouse sensitivity for spectator look.")]
    public float spectatorMouseSensitivity = 2.0f;

    [Header("Disable Gameplay On Death")]
    [Tooltip("Scripts to disable when dead (Interact, Pickup, Mic input, etc).")]
    public Behaviour[] disableOnDeath;

    [Tooltip("Disable PlayerInventory after dropping items.")]
    public bool disableInventoryAfterDrop = true;

    [Tooltip("Disable Sanity components.")]
    public bool disableSanity = true;

    [Header("Flashlight")]
    [Tooltip("If you use a Light on the camera as flashlight, we'll disable it on death.")]
    public Light flashlightLight;

    [Header("Post Processing")]
    [Tooltip("Spectator post process Volume object name in the scene.")]
    public string spectatorPostProcessingName = "SpectatorPostProcessing";

    [Tooltip("Optional gameplay volume to disable on death.")]
    public Volume gameplayVolumeToDisable;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool isDead = false;
    private GameObject spectatorGO;

    // -------------------------
    // PUBLIC ENTRY POINT (Sanity calls this)
    // -------------------------
    public void BeginDeath()
    {
        if (isDead) return;
        isDead = true;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        ResolveRefs();

        // 1) Drop all inventory
        PlayerInventory inv = GetComponentInChildren<PlayerInventory>(true);
        if (inv != null)
        {
            inv.DropAllItems();
            if (disableInventoryAfterDrop)
                inv.enabled = false;
        }

        // 2) Disable requested scripts (interaction/mic/etc)
        if (disableOnDeath != null)
        {
            for (int i = 0; i < disableOnDeath.Length; i++)
                if (disableOnDeath[i] != null) disableOnDeath[i].enabled = false;
        }

        // 3) Disable sanity
        if (disableSanity)
            DisableSanityComponents();

        // 4) Turn off flashlight
        if (flashlightLight == null && mainCamera != null)
            flashlightLight = mainCamera.GetComponentInChildren<Light>(true);

        if (flashlightLight != null)
            flashlightLight.enabled = false;

        // 5) Wait for death animation to play
        Log($"Waiting {ragdollDelay:0.00}s then ragdoll...");
        yield return new WaitForSeconds(Mathf.Max(0f, ragdollDelay));

        // 6) Enable ragdoll on model
        EnableRagdoll();

        // 7) Small delay then spectator swap
        yield return new WaitForSeconds(Mathf.Max(0f, spectatorDelayAfterRagdoll));

        ApplySpectatorPostProcessing();
        SpawnSpectatorAndMoveCamera();

        Log("Death -> ragdoll + spectator done.");
    }

    // -------------------------
    // Resolve refs safely
    // -------------------------
    void ResolveRefs()
    {
        if (mainCamera == null && Camera.main != null)
            mainCamera = Camera.main;

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);

        if (modelRoot == null && playerAnimator != null)
            modelRoot = playerAnimator.transform;

        // Try to find player collider root (your cylinder) if not assigned
        if (playerColliderRoot == null)
        {
            CharacterController cc = GetComponentInChildren<CharacterController>(true);
            if (cc != null) playerColliderRoot = cc.transform;
        }
    }

    // -------------------------
    // Ragdoll handling
    // -------------------------
    void EnableRagdoll()
    {
        if (modelRoot == null)
        {
            Log("EnableRagdoll: modelRoot is null (skipped).");
            return;
        }

        if (disableAnimatorOnRagdoll && playerAnimator != null)
            playerAnimator.enabled = false;

        // Disable main player controller collider so it doesn't fight physics
        if (playerColliderRoot != null)
        {
            if (disableCharacterControllerOnDeath)
            {
                CharacterController cc = playerColliderRoot.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }

            if (disableMainRigidbodyOnDeath)
            {
                Rigidbody rb = playerColliderRoot.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
        }

        // Enable ragdoll rigidbodies
        Rigidbody[] rbs = modelRoot.GetComponentsInChildren<Rigidbody>(true);
        Collider[] cols = modelRoot.GetComponentsInChildren<Collider>(true);

        // Turn on colliders (ragdoll bones)
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            // Don't mess with trigger colliders if you have them
            cols[i].enabled = true;
        }

        // Make ragdoll bodies physical
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null) continue;
            rbs[i].isKinematic = false;
            rbs[i].useGravity = true;
        }

        Log("Ragdoll enabled.");
    }

    // -------------------------
    // Spectator with collisions (NO BODY)
    // -------------------------
    void SpawnSpectatorAndMoveCamera()
    {
        if (mainCamera == null)
        {
            Log("SpawnSpectator: no mainCamera.");
            return;
        }

        // Make spectator capsule (invisible)
        spectatorGO = new GameObject("SpectatorController");
        spectatorGO.transform.position = mainCamera.transform.position;
        spectatorGO.transform.rotation = Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f);

        int spectatorLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (spectatorLayer != -1)
            spectatorGO.layer = spectatorLayer;
        else
            Log($"Spectator layer '{spectatorLayerName}' not found. Create it in Unity > Layers.");

        // Add CharacterController so you collide with floors/walls
        CharacterController cc = spectatorGO.AddComponent<CharacterController>();
        cc.radius = spectatorRadius;
        cc.height = spectatorHeight;
        cc.center = new Vector3(0f, spectatorHeight * 0.5f, 0f);
        cc.stepOffset = 0.35f;
        cc.minMoveDistance = 0f;

        // Add motor
        SpectatorMotor motor = spectatorGO.AddComponent<SpectatorMotor>();
        motor.cc = cc;
        motor.moveSpeed = spectatorMoveSpeed;
        motor.sprintMultiplier = spectatorSprintMultiplier;
        motor.gravity = spectatorGravity;
        motor.mouseSensitivity = spectatorMouseSensitivity;

        // Detach camera from player/model and attach to spectator
        if (attachCameraToSpectator)
        {
            Transform camT = mainCamera.transform;

            // Ensure camera doesn't have gravity physics messing it up
            Rigidbody camRb = camT.GetComponent<Rigidbody>();
            if (camRb != null)
            {
                camRb.useGravity = false;
                camRb.isKinematic = true;
            }

            camT.SetParent(spectatorGO.transform, true);

            // Keep camera at head-ish height relative to capsule
            camT.localPosition = new Vector3(0f, spectatorHeight * 0.88f, 0f);
        }

        // Hide the living "player view body" if you have any renderers still around for the local player
        // We DO NOT destroy the ragdoll (corpse should remain)
        HideLocalFirstPersonBodyRenderers();

        Log("Spectator spawned with collisions.");
    }

    void HideLocalFirstPersonBodyRenderers()
    {
        // This prevents you from “having a body” as spectator.
        // Corpse ragdoll is still visible because it’s separate physical bones/renderers on modelRoot.
        // If your modelRoot renderers are the corpse you WANT visible, do NOT disable them.

        // If you have a separate first-person arms/body object, add it to disableOnDeath instead.
        // Here we ONLY try to disable any Renderer directly under the PLAYER ROOT excluding modelRoot.
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;

            // Don't hide the corpse mesh (modelRoot)
            if (modelRoot != null && rends[i].transform.IsChildOf(modelRoot)) continue;

            rends[i].enabled = false;
        }
    }

    // -------------------------
    // Sanity disabling
    // -------------------------
    void DisableSanityComponents()
    {
        MonoBehaviour[] monos = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null) continue;
            if (monos[i].GetType().Name == "Sanity")
                monos[i].enabled = false;
        }
    }

    // -------------------------
    // Post processing
    // -------------------------
    void ApplySpectatorPostProcessing()
    {
        if (gameplayVolumeToDisable != null)
            gameplayVolumeToDisable.enabled = false;

        Volume spectatorVol = FindVolumeByName(spectatorPostProcessingName);
        if (spectatorVol != null)
        {
            spectatorVol.enabled = true;
            spectatorVol.weight = 1f;
            Log($"Enabled spectator post processing: {spectatorVol.name}");
        }
        else
        {
            Log($"Could not find Volume named '{spectatorPostProcessingName}'.");
        }
    }

    Volume FindVolumeByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;

        Volume[] vols = Resources.FindObjectsOfTypeAll<Volume>();
        for (int i = 0; i < vols.Length; i++)
        {
            if (vols[i] == null) continue;
            if (vols[i].name == exactName)
                return vols[i];
        }
        return null;
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log("[Death] " + msg, this);
    }
}

// ----------------------------------------------------
// Spectator motor: collision + gravity (won’t fall through map)
// ----------------------------------------------------
public class SpectatorMotor : MonoBehaviour
{
    [HideInInspector] public CharacterController cc;

    public float moveSpeed = 5.5f;
    public float sprintMultiplier = 1.6f;
    public float gravity = 18f;
    public float mouseSensitivity = 2.0f;

    private float yaw;
    private float pitch;
    private float verticalVelocity;

    void Start()
    {
        yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        if (cc == null) return;

        // Look
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Camera pitch is handled by the camera itself (it’s parented).
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Movement
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= sprintMultiplier;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;

        if (move.sqrMagnitude > 1f) move.Normalize();

        // Gravity (keeps you grounded; prevents “falling through” by uncontrolled physics)
        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        Vector3 final = move * speed;
        final.y = verticalVelocity;

        cc.Move(final * Time.deltaTime);
    }
}
