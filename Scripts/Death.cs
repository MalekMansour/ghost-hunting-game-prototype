using System.Collections;
using UnityEngine;

public class Death : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Animator on the player model. If empty, auto-find in children.")]
    public Animator playerAnimator;

    [Tooltip("Trigger parameter name for death animation.")]
    public string deathTriggerName = "Die";

    [Tooltip("Optionally turn on root motion during death so the animation can drop the body to the floor (IF the clip has root motion).")]
    public bool enableRootMotionDuringDeath = true;

    [Tooltip("Wait this long before freezing the animation + switching to spectator.")]
    public float deathWaitSeconds = 3f;

    [Header("Death Sound")]
    public AudioSource deathAudioSource;
    public AudioClip deathClip;
    [Range(0f, 5f)] public float deathVolume = 1f;

    [Header("Disable These Scripts On Death (drag anything here)")]
    [Tooltip("Put PlayerMovement, Footsteps, PlayerInventory, Interaction, Flashlight script, PlayerRaycaster, etc here.")]
    public Behaviour[] scriptsToDisable;

    [Header("Layer Switching")]
    public string playerLayerName = "Player";
    public string spectatorLayerName = "Spectator";

    [Tooltip("Change the layer recursively on THIS whole player hierarchy.")]
    public bool setWholeHierarchyToSpectatorLayer = true;

    [Header("Camera / Spectator Controller")]
    [Tooltip("Main camera. If empty, uses Camera.main.")]
    public Camera mainCamera;

    [Tooltip("Create an invisible spectator capsule with collisions.")]
    public bool spawnSpectatorController = true;

    [Tooltip("Spectator controller name (created at runtime).")]
    public string spectatorObjectName = "SpectatorController";

    [Tooltip("Height of spectator CharacterController.")]
    public float spectatorHeight = 1.75f;

    [Tooltip("Radius of spectator CharacterController.")]
    public float spectatorRadius = 0.35f;

    [Tooltip("Speed of spectator movement.")]
    public float spectatorMoveSpeed = 5.5f;

    [Tooltip("Sprint multiplier (Shift).")]
    public float spectatorSprintMultiplier = 1.6f;

    [Tooltip("Gravity for spectator so you stay grounded.")]
    public float spectatorGravity = 18f;

    [Tooltip("Mouse sensitivity for spectator look.")]
    public float spectatorMouseSensitivity = 2.0f;

    [Tooltip("Keep camera at same WORLD position/rotation when switching.")]
    public bool keepCameraWorldTransform = true;

    [Tooltip("When parenting camera to spectator, keep the camera WORLD Y exactly the same (prevents camera dropping).")]
    public bool keepCameraWorldY = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool deadStarted = false;
    private GameObject spectatorGO;
    private bool cachedApplyRootMotion;
    private float cachedAnimatorSpeed;

    public void BeginDeath()
    {
        if (deadStarted) return;
        deadStarted = true;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        ResolveRefs();

        // 1) Trigger death animation
        if (playerAnimator != null)
        {
            cachedApplyRootMotion = playerAnimator.applyRootMotion;
            cachedAnimatorSpeed = playerAnimator.speed;

            if (enableRootMotionDuringDeath)
                playerAnimator.applyRootMotion = true;

            if (!string.IsNullOrEmpty(deathTriggerName))
            {
                playerAnimator.ResetTrigger(deathTriggerName);
                playerAnimator.SetTrigger(deathTriggerName);
            }
        }
        else
        {
            Log("No Animator found.");
        }

        // 2) Play death sound
        if (deathAudioSource != null && deathClip != null)
        {
            // Keep it local (multiplayer-safe later). You can make it 3D on a separate world AudioSource if needed.
            deathAudioSource.spatialBlend = 0f;
            deathAudioSource.PlayOneShot(deathClip, Mathf.Clamp(deathVolume, 0f, 5f));
        }
        else
        {
            Log("Death sound skipped (missing AudioSource or clip).");
        }

        // 3) Wait 3 seconds
        yield return new WaitForSeconds(Mathf.Max(0f, deathWaitSeconds));

        // 4) Freeze the animation on its last frame
        FreezeAnimatorOnLastFrame();

        // 5) Disable scripts (you decide in inspector)
        DisableScripts();

        // 6) Swap layer to Spectator (so ghosts ignore you)
        ApplySpectatorLayer();

        // 7) Camera: detach + spectator movement with collisions
        if (spawnSpectatorController)
            CreateSpectatorControllerAndAttachCamera();
        else
            DetachCameraOnly();

        Log("Death complete -> spectator active.");
    }

    private void ResolveRefs()
    {
        if (mainCamera == null && Camera.main != null)
            mainCamera = Camera.main;

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);

        if (deathAudioSource == null)
            deathAudioSource = GetComponentInChildren<AudioSource>(true);
    }

    private void FreezeAnimatorOnLastFrame()
    {
        if (playerAnimator == null) return;

        // Force the current state to its end and freeze.
        // NOTE: This assumes the state is on layer 0 and the death clip is the active state after 3 seconds.
        // If your controller uses a different layer, tell me and I’ll adapt.
        playerAnimator.Update(0f); // ensure fresh state info
        AnimatorStateInfo st = playerAnimator.GetCurrentAnimatorStateInfo(0);

        // Snap to end of current state and freeze there
        playerAnimator.Play(st.shortNameHash, 0, 1f);
        playerAnimator.Update(0f);

        playerAnimator.speed = 0f;

        // Keep root motion setting as-is (we want the body where it ended up)
        Log("Animator frozen on last frame.");
    }

    private void DisableScripts()
    {
        if (scriptsToDisable == null) return;

        for (int i = 0; i < scriptsToDisable.Length; i++)
        {
            if (scriptsToDisable[i] == null) continue;
            scriptsToDisable[i].enabled = false;
        }

        Log("Disabled scripts from inspector list.");
    }

    private void ApplySpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer == -1)
        {
            Log($"Spectator layer '{spectatorLayerName}' not found. Create it in Project Settings > Tags and Layers.");
            return;
        }

        if (!setWholeHierarchyToSpectatorLayer)
        {
            gameObject.layer = specLayer;
            return;
        }

        SetLayerRecursively(transform, specLayer);
        Log("Set player hierarchy to Spectator layer.");
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private void DetachCameraOnly()
    {
        if (mainCamera == null) return;
        Transform camT = mainCamera.transform;

        // Detach, keep world transform
        if (keepCameraWorldTransform)
        {
            Vector3 wp = camT.position;
            Quaternion wr = camT.rotation;
            camT.SetParent(null, true);
            camT.position = wp;
            camT.rotation = wr;
        }
        else
        {
            camT.SetParent(null, true);
        }
    }

    private void CreateSpectatorControllerAndAttachCamera()
    {
        if (mainCamera == null) return;

        Transform camT = mainCamera.transform;

        Vector3 camWorldPos = camT.position;
        Quaternion camWorldRot = camT.rotation;

        // Create spectator object
        spectatorGO = new GameObject(spectatorObjectName);

        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer != -1) spectatorGO.layer = specLayer;

        // Spawn at camera position (so you don't teleport weirdly)
        spectatorGO.transform.position = camWorldPos;
        spectatorGO.transform.rotation = Quaternion.Euler(0f, camWorldRot.eulerAngles.y, 0f);

        // Add collisions
        CharacterController cc = spectatorGO.AddComponent<CharacterController>();
        cc.height = spectatorHeight;
        cc.radius = spectatorRadius;
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

        // Parent camera to spectator BUT keep its world pos (and Y if desired)
        Vector3 targetWorldPos = camWorldPos;
        if (keepCameraWorldY)
        {
            // Keep the SAME Y you already had
            targetWorldPos.y = camWorldPos.y;
        }

        // Detach first
        camT.SetParent(null, true);
        camT.position = camWorldPos;
        camT.rotation = camWorldRot;

        // Parent now
        camT.SetParent(spectatorGO.transform, true);

        // Restore world transform exactly (prevents camera "dropping" due to local pos changes)
        camT.position = targetWorldPos;
        camT.rotation = camWorldRot;

        Log("Spectator controller created + camera attached (collision safe).");
    }

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log("[Death] " + msg, this);
    }
}

// ---- Simple spectator movement with collisions ----
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

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Move
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;

        if (move.sqrMagnitude > 1f) move.Normalize();

        // Gravity
        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        Vector3 vel = move * speed;
        vel.y = verticalVelocity;

        cc.Move(vel * Time.deltaTime);
    }
}
