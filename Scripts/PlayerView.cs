using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public float sensitivity = 150f;
    public float smoothTime = 0.05f;
    public Transform playerBody;

    [Header("Look Limits")]
    public float maxLookUp = 90f;    
    public float maxLookDown = -40f;  

    float xRotation = 0f;

    float currentMouseX;
    float currentMouseY;
    float mouseXVelocity;
    float mouseYVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        playerBody.Rotate(Vector3.up * currentMouseX * Time.deltaTime);
    }
}

