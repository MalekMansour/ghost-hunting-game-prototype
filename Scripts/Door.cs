using UnityEngine;
using System.Collections;

public class Door : MonoBehaviour
{
    [Header("Door Parts")]
    [Tooltip("The transform that rotates (usually the hinge/pivot object).")]
    public Transform hinge;

    [Header("Open Settings")]
    public float openAngle = 90f;
    public float openCloseTime = 0.2f;
    public bool startOpen = false;

    [Header("Optional Sound")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    bool isOpen;
    bool isBusy;
    Quaternion closedRot;
    Quaternion openRot;

    void Awake()
    {
        if (hinge == null) hinge = transform;

        closedRot = hinge.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);

        isOpen = startOpen;
        hinge.localRotation = isOpen ? openRot : closedRot;
    }

    public void Toggle()
    {
        if (isBusy) return;
        StartCoroutine(AnimateDoor(!isOpen));
    }

    IEnumerator AnimateDoor(bool open)
    {
        isBusy = true;

        // sound
        if (audioSource != null)
        {
            AudioClip clip = open ? openSound : closeSound;
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        Quaternion from = hinge.localRotation;
        Quaternion to = open ? openRot : closedRot;

        float t = 0f;
        float dur = Mathf.Max(0.01f, openCloseTime);

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            hinge.localRotation = Quaternion.Slerp(from, to, a);
            yield return null;
        }

        hinge.localRotation = to;
        isOpen = open;
        isBusy = false;
    }
}
