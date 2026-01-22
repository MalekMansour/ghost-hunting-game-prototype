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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip outOfBreathSound;
    [Range(0f, 1f)] public float breathVolume = 0.3f;

    [Header("Anti-Slide")]
    [Tooltip("If no input and Rigidbody is drifting, freeze X/Z so player can't slide.")]
    public bool preventSlidingWhenNoInput = true;

    [Tooltip("How fast you must be drifting (XZ) before we lock. (Try 0.05 to 0.2)")]
    public float slideVelocityThreshold = 0.08f;

    [Tooltip("How long there must be no input before we lock (prevents locking during tiny stops).")]
    public float noInputLockDelay = 0.05f;

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

    [HideInInspector] public Vector2 moveInput;

    private Rigidbody rb;

    // lock Y to starting height forever
    private float lockedY;

    // anti-slide state
    private float noInputTimer = 0f;
    private bool slideLockedXZ = false;

    private bool didInit = false;

    public override void OnNetworkSpawn()
    {
        if (!didInit)
        {
            InitOnce();
        }

        // Non-owners should NOT run input or movement.
        // They'll be moved by NetworkTransform syncing the root.
    }

    private void Start()
    {
        if (!didInit)
        {
            InitOnce();
        }
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
        // Only the owning client reads input
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

        // If player gives input again, instantly unlock XZ
        if (preventSlidingWhenNoInput && slideLockedXZ && moveInput.sqrMagnitude > 0.01f)
        {
            UnlockXZ();
        }
    }

    void FixedUpdate()
    {
        // Only the owning client moves the Rigidbody
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner)
            return;

        if (isMovementLocked)
            return;

        if (rb == null)
            return;

        Vector3 moveDir =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 targetPos =
            rb.position + moveDir.normalized * currentSpeed * Time.fixedDeltaTime;

        targetPos.y = lockedY;

        if (preventSlidingWhenNoInput)
        {
            bool hasInput = moveInput.sqrMagnitude > 0.01f;

            if (!hasInput)
            {
                noInputTimer += Time.fixedDeltaTime;

                if (noInputTimer >= noInputLockDelay)
                {
                    Vector3 v = rb.linearVelocity;
                    float xzSpeed = new Vector2(v.x, v.z).magnitude;

                    if (!slideLockedXZ && xzSpeed > slideVelocityThreshold)
                    {
                        LockXZ();
                    }
                }
            }
            else
            {
                noInputTimer = 0f;
                if (slideLockedXZ)
                    UnlockXZ();
            }

            if (!slideLockedXZ)
            {
                rb.MovePosition(targetPos);
            }
        }
        else
        {
            rb.MovePosition(targetPos);
        }

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        if (cylinderFollowsRoot && cylinder != null)
        {
            cylinder.position = rb.position;
            cylinder.rotation = transform.rotation;
        }
    }

    void LockXZ()
    {
        slideLockedXZ = true;

        rb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotation;

        Vector3 v = rb.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    void UnlockXZ()
    {
        slideLockedXZ = false;
        noInputTimer = 0f;

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
