using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuMusicController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource introSource; // plays once
    [SerializeField] private AudioSource loopSource;  // loops forever

    [Header("Clips")]
    [SerializeField] private AudioClip introClip;
    [SerializeField] private AudioClip loopClip;

    [Header("Timing")]
    [Tooltip("When (in seconds) to start the loop track while intro continues/finishes.")]
    [SerializeField] private float loopStartTime = 112f; // 1:52

    [Header("Optional")]
    [Tooltip("Stop intro once loop starts (recommended if you don't want overlap).")]
    [SerializeField] private bool stopIntroWhenLoopStarts = true;

    private bool loopStarted = false;

    private void Start()
    {
        // Safety
        if (introSource == null || loopSource == null)
        {
            Debug.LogError("[MainMenuMusicController] Missing AudioSource references.");
            enabled = false;
            return;
        }

        introSource.clip = introClip;
        introSource.loop = false;
        introSource.playOnAwake = false;

        loopSource.clip = loopClip;
        loopSource.loop = true;
        loopSource.playOnAwake = false;

        loopStarted = false;

        // Start intro immediately
        if (introClip != null)
            introSource.Play();
        else
            Debug.LogWarning("[MainMenuMusicController] Intro clip is not assigned.");
    }

    private void Update()
    {
        if (loopStarted) return;
        if (!introSource.isPlaying) return; // intro hasn't started / already ended

        if (introSource.time >= loopStartTime)
        {
            loopStarted = true;

            if (loopClip != null)
                loopSource.Play();
            else
                Debug.LogWarning("[MainMenuMusicController] Loop clip is not assigned.");

            if (stopIntroWhenLoopStarts)
                introSource.Stop();
        }
    }

    // Optional helper you can call from your Play button
    public void StopAllMenuMusic()
    {
        if (introSource != null) introSource.Stop();
        if (loopSource != null) loopSource.Stop();
        loopStarted = false;
    }
}
