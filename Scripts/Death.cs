using UnityEngine;
using System.Collections;

/// <summary>
/// Death flow (what you asked for):
/// 1) Call BeginDeath() when sanity reaches 0 (from your Sanity system).
/// 2) Trigger "Die" animation + play death sound.
/// 3) Wait 3 seconds.
/// 4) Freeze the Animator on the last frame (player stays dropped).
/// 5) Change WHOLE player from layer Player -> Spectator (recursively).
/// 6) Disable ALL scripts on the PLAYER CYLINDER (every MonoBehaviour).
/// 7) Spawn a spectator controller that moves the Main Camera with collisions
///    and keeps the camera at the same world Y (no dropping).
/// </summary>
public class Death : MonoBehaviour
{
    [Header("How death is triggered")]
    [Tooltip("IMPORTANT: You said death happens when sanity hits 0. So call BeginDeath() from your Sanity.cs when sanity <= 0.")]
    public bool autoTriggerDisabled = true; // kept only as a reminder; this script does NOT auto-check your Sanity type.

    [Header("References")]
    [Tooltip("Drag your PLAYER CYLINDER here (the object that has ALL the player scripts). If empty, this GameObject is used.")]
    public GameObject playerCylinder;

    [Tooltip("Animator that contains the Die trigger (often on the model child).")]
    public Animator animator;

    [Tooltip("AudioSource used to play death sound (can be on cylinder).")]
    public AudioSource audioSource;

    [Tooltip("Main camera to move in spectator. If empty, Camera.main is used.")]
    public Camera mainCamera;

    [Header("Animation")]
    [Tooltip("Animator trigger parameter name.")]
    public string dieTrigger = "Die";

    [Tooltip("Animator layer index (usually 0).")]
    public int animatorLayer = 0;

    [Tooltip("If your death clip uses root motion, enable this so the body drops properly.")]
    public bool enableRootMotionDuringDeath = true;

    [Tooltip("Optional: If you KNOW the exact state name for the death animation, put it here to freeze precisely at the end. Otherwise leave blank.")]
    public string dieStateName = "";

    [Header("Audio")]
    public AudioClip deathClip;

    [Header("Timing")]
    [Tooltip("Seconds to wait after triggering death before spectator mode begins.")]
    public float spectatorDelay = 3f;

    [Header("Layers")]
    public string playerLayerName = "Player";
    public string spectatorLayerName = "Spectator";

    [Header("Disable scripts")]
    [Tooltip("Disable every MonoBehaviour on the playerCylinder (except this Death script until the end).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Disable Death script too at the very end (after spectator is created).")]
    public bool disableDeathScriptToo = true;

    [Header("Spectator")]
    [Tooltip("If true, we spawn a spectator controller object with CharacterController collisions and parent the camera to it.")]
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
    public bool debugLogs = false;

    private bool deadStarted = false;

    private void Awake()
    {
        if (playerCylinder == null) playerCylinder = gameObject;
        if (mainCamera == null) mainCamera = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Call this from your Sanity.cs when sanity <= 0.
    /// </summary>
    public void BeginDeath()
    {
        if (deadStarted) return;
        deadStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Log("Death started");

        // Trigger animation
        if (animator != null)
        {
            animator.speed = 1f;
            animator.applyRootMotion = enableRootMotionDuringDeath;

            animator.ResetTrigger(dieTrigger);
            animator.SetTrigger(dieTrigger);
        }

        // Play sound
        if (audioSource != null && deathClip != null)
        {
            audioSource.PlayOneShot(deathClip);
        }

        // Wait 3 seconds (or whatever you set)
        yield return new WaitForSeconds(spectatorDelay);

        // Freeze on last frame so body stays on floor
        FreezeAnimatorOnLastFrame();

        // Switch player layers to Spectator (recursively)
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
        {
            SetLayerRecursively(gameObject, specLayer);
            Log($"Player layer -> {spectatorLayerName}");
        }
        else
        {
            Log($"Spectator layer '{spectatorLayerName}' not found. Create it in Unity Layers.");
        }

        // Disable ALL scripts on Cylinder
        DisableAllScriptsOnCylinder();

        // Create spectator controller with collisions and keep camera Y
        if (spawnSpectatorController)
        {
            CreateSpectatorController();
        }

        // Finally disable Death too (optional)
        if (disableDeathScriptToo)
        {
            this.enabled = false;
        }

        Log("Death routine complete. Spectator active.");
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
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            animator.Play(st.fullPathHash, animatorLayer, 1f);
            animator.Update(0f);
        }

        animator.speed = 0f; // freeze pose
        Log("Animator frozen on last frame.");
    }

    private void DisableAllScriptsOnCylinder()
    {
        if (!disableAllScriptsOnCylinder) return;

        if (playerCylinder == null) playerCylinder = gameObject;

        MonoBehaviour[] all = playerCylinder.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;

            // Keep this Death script alive until spectator is created
            if (mb == this) continue;

            mb.enabled = false;
        }

        Log("Disabled ALL MonoBehaviours on playerCylinder (except Death temporarily).");
    }

    private void CreateSpectatorController()
    {
        if (mainCamera == null)
        {
            Log("No main camera found. Assign mainCamera or tag your camera MainCamera.");
            return;
        }

        Transform camT = mainCamera.transform;

        // Store camera world pose BEFORE parenting
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        // Create controller object at camera position
        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camWorldPos;
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        // Put controller on Spectator layer too (optional but nice)
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0) SetLayerRecursively(spec, specLayer);

        // Collisions
        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        // Parent camera to controller while KEEPING world transform
        camT.SetParent(spec.transform, true);
        camT.position = camWorldPos;
        camT.rotation = camWorldRot;

        // Add controller script
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

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[Death] " + msg, this);
    }
}

/// <summary>
/// Spectator movement controller:
/// - Moves with CharacterController (collisions)
/// - Mouse look
/// - Keeps camera at fixed world Y (no dropping) if enabled
/// IMPORTANT: We fully-qualify UnityEngine.Cursor in case your project has its own Cursor class.
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
            // Fully qualified to avoid conflicts if you have your own Cursor class.
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (cc == null || cameraTransform == null) return;

        // Mouse look
        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // WASD move
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        input = Vector3.ClampMagnitude(input, 1f);

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 move = (transform.right * input.x + transform.forward * input.z) * speed;

        cc.Move(move * Time.deltaTime);

        // Keep camera at fixed Y so it never drops
        if (keepWorldY)
        {
            Vector3 p = cameraTransform.position;
            cameraTransform.position = new Vector3(p.x, fixedWorldY, p.z);
        }
    }
}
