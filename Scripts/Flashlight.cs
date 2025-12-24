using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;
    public float holdTime = 0.5f;

    [Header("Sound")]
    public AudioClip flashlightOnSound;
    public AudioClip flashlightOffSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    private float holdTimer;
    private bool flashlightOn = false;

    private Light cameraLight;
    private AudioSource audioSource;

    void Start()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Main Camera not found!");
            return;
        }

        cameraLight = cam.GetComponent<Light>();
        if (cameraLight == null)
        {
            Debug.LogError("No Light component found on Main Camera!");
            return;
        }

        audioSource = cam.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = cam.gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        cameraLight.enabled = false;
        flashlightOn = false;
    }

    void Update()
    {
        if (cameraLight == null)
            return;

        if (Input.GetKey(toggleKey))
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                ToggleFlashlight();
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    void ToggleFlashlight()
    {
        flashlightOn = !flashlightOn;
        cameraLight.enabled = flashlightOn;

        if (flashlightOn && flashlightOnSound != null)
        {
            audioSource.PlayOneShot(flashlightOnSound, volume);
        }
        else if (!flashlightOn && flashlightOffSound != null)
        {
            audioSource.PlayOneShot(flashlightOffSound, volume);
        }
    }
}
