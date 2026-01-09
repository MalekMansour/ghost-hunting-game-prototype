using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
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

    // ✅ NEW: Anti-slide lock
    [Header("Anti-Slide")]
    [Tooltip("If no input and Rigidbody is drifting, freeze X/Z so player can't slide.")]
    public bool preventSlidingWhenNoInput = true;

    [Tooltip("How fast you must be drifting (XZ) before we lock. (Try 0.05 to 0.2)")]
    public float slideVelocityThreshold = 0.08f;

    [Tooltip("How long there must be no input before we lock (prevents locking during tiny stops).")]
    public float noInputLockDelay = 0.05f;

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

    // ✅ lock Y to the starting height forever
    private float lockedY;

    // ✅ NEW anti-slide state
    private float noInputTimer = 0f;
    private bool slideLockedXZ = false;

    void Start()
    {
        currentStamina = maxStamina;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        lockedY = rb.position.y;

        // Keep your constraint behavior
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        if (playerCamera == null)
            Debug.LogError("Player camera NOT assigned on PlayerMovement!");
    }

    void Update()
    {
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

        if (playerCamera != null)
        {
            float targetHeight = isCrouching ? crouchCamHeight : standingCamHeight;
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetHeight, Time.deltaTime * camSmoothSpeed);
            playerCamera.localPosition = camPos;
        }

        // ✅ NEW: If player gives input again, instantly unlock XZ
        if (preventSlidingWhenNoInput && slideLockedXZ && moveInput.sqrMagnitude > 0.01f)
        {
            UnlockXZ();
        }
    }

    void FixedUpdate()
    {
        if (isMovementLocked)
            return;

        // --- Your original movement ---
        Vector3 moveDir =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 targetPos =
            rb.position + moveDir.normalized * currentSpeed * Time.fixedDeltaTime;

        // Keep Y locked
        targetPos.y = lockedY;

        // ✅ NEW: anti-slide logic
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
                // if we had locked, Update() also unlocks immediately, but this is extra-safe
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
