using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float sprintSpeed = 16f;
    public float crouchSpeed = 4f;

    [Header("Stamina")]
    public float maxStamina = 10f;
    public float staminaRegenRate = 3f;
    public float staminaDrainRate = 1f;
    public float sprintCooldown = 6f;

    [Header("Camera")]
    public Transform playerCamera;
    public float standingCamHeight = 1.6f;
    public float crouchCamHeight = 0.6f;
    public float camSmoothSpeed = 8f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip outOfBreathSound;
    [Range(0f, 1f)] public float breathVolume = 0.3f;

    // ✅ Anti-slide lock
    [Header("Anti-Slide")]
    [Tooltip("If no input and Rigidbody is drifting, freeze X/Z so player can't slide.")]
    public bool preventSlidingWhenNoInput = true;

    [Tooltip("How fast you must be drifting (XZ) before we lock. (Try 0.05 to 0.2)")]
    public float slideVelocityThreshold = 0.08f;

    [Tooltip("How long there must be no input before we lock (prevents locking during tiny stops).")]
    public float noInputLockDelay = 0.05f;

    // ✅ OPTIONAL: keep cylinder alive but synced to root for scripts that reference it
    [Header("Legacy Cylinder Support (optional)")]
    [Tooltip("If your project still uses a child 'Cylinder' object for references, assign it here. We'll keep it aligned with this root.")]
    public Transform cylinder;

    [Tooltip("If true, cylinder will follow this object's position/rotation each FixedUpdate.")]
    public bool cylinderFollowsRoot = false;

    private float currentSpeed;
    private float currentStamina;
    private bool canSprint = true;
    private bool isInSprintCooldown = false;
    private bool isMovementLocked = false;
    private bool isCrouching = false;

    private bool hasPlayedBreathSound = false;

    [HideInInspector]
    public Vector2 moveInput;

    private Rigidbody rb;

    // lock Y to starting height forever
    private float lockedY;

    // anti-slide state
    private float noInputTimer = 0f;
    private bool slideLockedXZ = false;

    // ✅ Added: so we don't spam logs / init multiple times
    private bool didInit = false;

    public override void OnNetworkSpawn()
    {
        // Initialize stamina for all instances (owner and non-owner) so UI calls won't break
        if (!didInit)
        {
            InitOnce();
        }

        // Owner should have camera reference; non-owners usually won't (their camera is disabled)
        // We only error on owner if camera missing.
        if (IsOwner && playerCamera == null)
            Debug.LogError("[PlayerMovement] Player camera NOT assigned on PlayerMovement (Owner).");

        // Non-owners should NOT run input or movement.
        // They'll be moved by NetworkTransform syncing the root.
    }

    private void Start()
    {
        // Keep compatibility if this script is used outside NGO context
        if (!didInit)
        {
            InitOnce();
        }

        if (playerCamera == null && (!NetworkManager.Singleton || (NetworkManager.Singleton && NetworkManager.Singleton.IsListening && IsOwner)))
            Debug.LogError("Player camera NOT assigned on PlayerMovement!");
    }

    private void InitOnce()
    {
        didInit = true;

        currentStamina = maxStamina;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[PlayerMovement] Rigidbody missing on the object that has PlayerMovement. " +
                           "For networking, this should be the Player ROOT (the NetworkObject).");
            return;
        }

        rb.freezeRotation = true;

        lockedY = rb.position.y;

        // Keep your constraint behavior
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
    }

    void Update()
    {
        // ✅ CRITICAL: only the owning client reads input / drives movement
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner)
            return;

        if (isMovementLocked)
            return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        moveInput = new Vector2(horizontal, vertical);

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (Input.GetKeyDown(KeyCode.LeftControl))
            isCrouching = !isCrouching;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (
            Input.GetKey(KeyCode.LeftShift) &&
            canSprint &&
            !isInSprintCooldown &&
            isMoving
        )
        {
            currentSpeed = sprintSpeed;
            currentStamina -= staminaDrainRate * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                canSprint = false;
                isInSprintCooldown = true;

                PlayOutOfBreathSound();

                StartCoroutine(SprintCooldown());
            }
        }
        else
        {
            currentSpeed = moveSpeed;

            if (!isInSprintCooldown && currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;

                if (currentStamina > maxStamina * 0.3f)
                    hasPlayedBreathSound = false;
            }
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        // Camera smoothing is owner-only (remote players don't need it)
        if (playerCamera != null)
        {
            float targetHeight = isCrouching ? crouchCamHeight : standingCamHeight;
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetHeight, Time.deltaTime * camSmoothSpeed);
            playerCamera.localPosition = camPos;
        }

        // If player gives input again, instantly unlock XZ
        if (preventSlidingWhenNoInput && slideLockedXZ && moveInput.sqrMagnitude > 0.01f)
        {
            UnlockXZ();
        }
    }

    void FixedUpdate()
    {
        // ✅ CRITICAL: only the owning client moves the Rigidbody
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner)
            return;

        if (isMovementLocked)
            return;

        if (rb == null)
            return;

        // --- Your original movement ---
        Vector3 moveDir =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 targetPos =
            rb.position + moveDir.normalized * currentSpeed * Time.fixedDeltaTime;

        // Keep Y locked
        targetPos.y = lockedY;

        // anti-slide logic
        if (preventSlidingWhenNoInput)
        {
            bool hasInput = moveInput.sqrMagnitude > 0.01f;

            if (!hasInput)
            {
                noInputTimer += Time.fixedDeltaTime;

                // Only lock after a tiny delay
                if (noInputTimer >= noInputLockDelay)
                {
                    // drift check on XZ
                    Vector3 v = rb.linearVelocity;
                    float xzSpeed = new Vector2(v.x, v.z).magnitude;

                    if (!slideLockedXZ && xzSpeed > slideVelocityThreshold)
                    {
                        LockXZ(); // freeze X/Z so physics can't slide you
                    }
                }
            }
            else
            {
                noInputTimer = 0f;
                // extra-safe unlock
                if (slideLockedXZ)
                    UnlockXZ();
            }

            // If locked, don't MovePosition (since X/Z are frozen anyway)
            if (!slideLockedXZ)
            {
                rb.MovePosition(targetPos);
            }
        }
        else
        {
            rb.MovePosition(targetPos);
        }

        // Your vertical velocity kill
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        // ✅ OPTIONAL: keep legacy cylinder aligned to root (if you still need it)
        if (cylinderFollowsRoot && cylinder != null)
        {
            cylinder.position = rb.position;
            cylinder.rotation = transform.rotation;
        }
    }

    void LockXZ()
    {
        slideLockedXZ = true;

        // Freeze X/Z in addition to your existing constraints
        rb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotation;

        // Kill sideways velocity immediately
        Vector3 v = rb.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    void UnlockXZ()
    {
        slideLockedXZ = false;
        noInputTimer = 0f;

        // Go back to your original constraints
        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotation;
    }

    void PlayOutOfBreathSound()
    {
        if (audioSource == null || outOfBreathSound == null)
            return;

        if (hasPlayedBreathSound)
            return;

        audioSource.PlayOneShot(outOfBreathSound, breathVolume);
        hasPlayedBreathSound = true;
    }

    IEnumerator SprintCooldown()
    {
        yield return new WaitForSeconds(sprintCooldown);
        isInSprintCooldown = false;
        canSprint = true;
    }

    public void SetMovementLocked(bool shouldLock)
    {
        isMovementLocked = shouldLock;
    }

    public bool IsCrouching()
    {
        return isCrouching;
    }

    public float GetStamina01()
    {
        return currentStamina / maxStamina;
    }

    public bool IsSprinting()
    {
        return
            Input.GetKey(KeyCode.LeftShift) &&
            canSprint &&
            !isInSprintCooldown &&
            moveInput.sqrMagnitude > 0.01f &&
            !isCrouching;
    }
}
