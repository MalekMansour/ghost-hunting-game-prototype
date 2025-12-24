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

    private float currentSpeed;
    private float currentStamina;
    private bool canSprint = true;
    private bool isInSprintCooldown = false;
    private bool isMovementLocked = false;

    private bool isCrouching = false;

    [HideInInspector]
    public Vector2 moveInput;

    private Rigidbody rb;

    void Start()
    {
        currentStamina = maxStamina;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (playerCamera == null)
        {
            Debug.LogError("Player camera NOT assigned on PlayerMovement!");
        }
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
        {
            isCrouching = !isCrouching;
        }

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
                StartCoroutine(SprintCooldown());
            }
        }
        else
        {
            currentSpeed = moveSpeed;

            if (!isInSprintCooldown && currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
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

        rb.MovePosition(targetPos);
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
}
