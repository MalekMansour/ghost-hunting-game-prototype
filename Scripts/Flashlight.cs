using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;
    public float holdTime = 0.5f;
    public bool IsOn => cameraLight != null && cameraLight.enabled;

    [Header("Sound")]
    public AudioClip flashlightOnSound;
    public AudioClip flashlightOffSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    private float holdTimer;
    private bool flashlightOn = false;

    private Light cameraLight;
    private AudioSource audioSource;

    private Camera cam;

    void Start()
    {
        TrySetup();
    }

    void Update()
    {
        // If camera/light wasn't ready yet (network spawn, scene load), keep trying
        if (cameraLight == null)
        {
            TrySetup();
            return;
        }

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

    private void TrySetup()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        // 🔧 FIX: find Light on camera OR any child (inactive included)
        if (cameraLight == null)
            cameraLight = cam.GetComponentInChildren<Light>(true);

        if (cameraLight == null)
            return;

        audioSource = cam.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = cam.gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        cameraLight.enabled = false;
        flashlightOn = false;
    }

    void ToggleFlashlight()
    {
        if (cameraLight == null)
            return;

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
