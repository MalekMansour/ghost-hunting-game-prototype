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

    // ✅ NEW: lock Y to the starting height forever
    private float lockedY;

    void Start()
    {
        currentStamina = maxStamina;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // ✅ NEW: record starting Y and freeze Y motion
        lockedY = rb.position.y;
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

                // Reset sound trigger once stamina comes back
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
    }

    void FixedUpdate()
    {
        if (isMovementLocked)
            return;

        Vector3 moveDir =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 targetPos =
            rb.position + moveDir.normalized * currentSpeed * Time.fixedDeltaTime;

        // ✅ NEW: force Y to stay locked (even if something tries to push)
        targetPos.y = lockedY;

        rb.MovePosition(targetPos);

        // ✅ NEW: also kill any leftover vertical velocity just in case
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;
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
