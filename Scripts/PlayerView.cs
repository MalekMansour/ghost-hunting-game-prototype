using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;
    public Transform playerBody;

    [Header("Look Limits")]
    public float maxLookUp = 90f;
    public float maxLookDown = -40f;

    [Header("Post Processing (Toggle Components)")]
    [Tooltip("Your normal post processing component (enabled while alive).")]
    public Behaviour NormalPP;

    [Tooltip("Your spectator post processing component (disabled by default, enabled when dead).")]
    public Behaviour SpectatorPP;

    float xRotation = 0f;

    float currentMouseX;
    float currentMouseY;
    float mouseXVelocity;
    float mouseYVelocity;

    private bool spectatorMode = false;

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        // ensure defaults
        if (NormalPP != null) NormalPP.enabled = true;
        if (SpectatorPP != null) SpectatorPP.enabled = false;
    }

    public void SetSpectatorMode(bool enabled)
    {
        spectatorMode = enabled;

        if (spectatorMode)
        {
            // camera no longer controls body rotation
            playerBody = null;

            // toggle PP
            if (NormalPP != null) NormalPP.enabled = false;
            if (SpectatorPP != null) SpectatorPP.enabled = true;
        }
        else
        {
            if (NormalPP != null) NormalPP.enabled = true;
            if (SpectatorPP != null) SpectatorPP.enabled = false;
        }
    }

    void Update()
    {
        float targetMouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float targetMouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        currentMouseX = Mathf.SmoothDamp(currentMouseX, targetMouseX, ref mouseXVelocity, smoothTime);
        currentMouseY = Mathf.SmoothDamp(currentMouseY, targetMouseY, ref mouseYVelocity, smoothTime);

        xRotation -= currentMouseY * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (!spectatorMode && playerBody != null)
        {
            playerBody.Rotate(Vector3.up * currentMouseX * Time.deltaTime);
        }
    }
}
