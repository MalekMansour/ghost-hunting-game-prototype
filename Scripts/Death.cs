using UnityEngine;
using System.Collections;

public class Death : MonoBehaviour
{
    [Header("Sanity")]
    [Tooltip("Optional: call BeginDeath() yourself from your sanity system. If assigned, this script will auto-check it.")]
    public Sanity sanity;               
    public float sanityZeroThreshold = 0f;

    [Header("Animation")]
    public Animator animator;        
    [Tooltip("Animator trigger parameter name.")]
    public string dieTrigger = "Die";

    [Tooltip("Optional: If you want to hard-jump to the end of a specific state, put its name here. Leave empty to use current state.")]
    public string dieStateName = "";  

    [Tooltip("Animator layer index for the death state (usually 0).")]
    public int animatorLayer = 0;

    [Tooltip("Enable root motion during death if your clip uses root motion.")]
    public bool enableRootMotionDuringDeath = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip deathClip;

    [Header("Timings")]
    [Tooltip("Wait this long after triggering death before spectator starts.")]
    public float spectatorDelay = 3f;

    [Header("Layers")]
    public string playerLayerName = "Player";
    public string spectatorLayerName = "Spectator";

    [Header("Disable ALL scripts on Cylinder")]
    [Tooltip("Drag your PLAYER CYLINDER (the object that has all player scripts) here. If empty, this GameObject is used.")]
    public GameObject playerCylinder;

    [Tooltip("Disable every MonoBehaviour on the Cylinder (except this Death script until the end).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Also disable the Death script itself at the very end (after spectator is created).")]
    public bool disableDeathScriptToo = true;

    [Header("Camera / Spectator")]
    [Tooltip("Main camera to control. If empty, Camera.main is used.")]
    public Camera mainCamera;

    [Tooltip("If true, we spawn a spectator controller and parent the camera to it.")]
    public bool spawnSpectatorController = true;

    [Tooltip("Keep the camera at the SAME world Y level during spectator (prevents dropping).")]
    public bool keepCameraWorldY = true;

    [Tooltip("Height offset for spectator controller to match camera position nicely.")]
    public float spectatorControllerHeight = 1.8f;

    [Tooltip("CharacterController radius for collisions.")]
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

    private void Update()
    {
        // Optional auto-check if you assign sanity script here
        if (!deadStarted && sanity != null)
        {
            if (sanity.currentSanity <= sanityZeroThreshold)
                BeginDeath();
        }
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

        // Animation: trigger Die
        if (animator != null)
        {
            animator.speed = 1f;
            animator.applyRootMotion = enableRootMotionDuringDeath;

            // Clear + set trigger (helps reliability)
            animator.ResetTrigger(dieTrigger);
            animator.SetTrigger(dieTrigger);
        }

        // Sound
        if (audioSource != null && deathClip != null)
        {
            audioSource.PlayOneShot(deathClip);
        }

        // Wait before spectator
        yield return new WaitForSeconds(spectatorDelay);

        // Freeze on last frame so body stays on floor
        FreezeAnimatorOnLastFrame();

        // Switch entire player to spectator layer (so you can change collision rules if desired)
        SetLayerRecursively(gameObject, LayerMask.NameToLayer(spectatorLayerName));
        Log($"Player layer -> {spectatorLayerName}");

        // Disable ALL scripts on Cylinder
        DisableAllScriptsOnCylinder();

        // Spawn spectator controller + attach camera
        if (spawnSpectatorController)
        {
            CreateSpectatorController();
        }

        // Finally disable Death script too (optional)
        if (disableDeathScriptToo)
        {
            this.enabled = false;
        }

        Log("Death routine complete. Spectator active.");
    }

    private void FreezeAnimatorOnLastFrame()
    {
        if (animator == null) return;

        // Try to jump to the end of a known death state if provided
        if (!string.IsNullOrEmpty(dieStateName))
        {
            animator.Play(dieStateName, animatorLayer, 1f);
            animator.Update(0f);
        }
        else
        {
            // Otherwise, freeze whatever state we're currently in at the end
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            animator.Play(st.fullPathHash, animatorLayer, 1f);
            animator.Update(0f);
        }

        animator.speed = 0f; // freezes on last frame
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

            // Keep this alive until spectator is created
            if (mb == this) continue;

            mb.enabled = false;
        }

        Log("Disabled ALL MonoBehaviours on Cylinder (except Death temporarily).");
    }

    private void CreateSpectatorController()
    {
        if (mainCamera == null)
        {
            Log("No mainCamera found; spectator not created.");
            return;
        }

        // Remember camera world transform
        Transform camT = mainCamera.transform;
        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        // Create controller object at camera position (optionally offset down to place CC properly)
        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = new Vector3(camWorldPos.x, camWorldPos.y, camWorldPos.z);
        spec.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        // Put controller on Spectator layer
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0) SetLayerRecursively(spec, specLayer);

        // Add CharacterController for collisions
        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        // Parent camera to controller and restore world transform
        camT.SetParent(spec.transform, true);
        camT.position = camWorldPos;
        camT.rotation = camWorldRot;

        // Add movement/look script
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
        if (layer < 0) return;

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
            Debug.Log($"[Death] {msg}", this);
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

        // Movement
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

public class Sanity : MonoBehaviour
{
    public float currentSanity = 100f;
}
