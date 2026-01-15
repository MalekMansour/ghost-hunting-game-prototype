using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Journal : MonoBehaviour
{
    private enum Spread { OneTwo = 0, ThreeFour = 1, FiveSix = 2 }

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("Menu Cursor (your custom cursor GO)")]
    [SerializeField] private GameObject menuCursorGO;

    [Header("Player Lock While Journal Open")]
    [SerializeField] private MonoBehaviour[] playerScriptsToDisable;
    [SerializeField] private MonoBehaviour[] lookScriptsToDisable;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private bool freezeTime = false;

    [Header("Front Cover (CLICK THIS)")]
    [SerializeField] private GameObject frontCoverGO;       // FrontCover object
    [SerializeField] private RectTransform frontCoverRect;  // FrontCover RectTransform
    [SerializeField] private Button frontCoverButton;       // FrontCover Button (optional, can auto-find)

    [Header("Open Book Base (hidden until cover finishes turning)")]
    [SerializeField] private GameObject openBookGO;         // OpenBook root (contains pages/backpages/crease)
    [SerializeField] private GameObject backPagesGO;        // BackPages (inside OpenBook)
    [SerializeField] private GameObject creaseGO;           // Crease (inside OpenBook)

    [Header("Pages (inside OpenBook)")]
    [SerializeField] private GameObject page1;
    [SerializeField] private GameObject page2;
    [SerializeField] private GameObject page3;
    [SerializeField] private GameObject page4;
    [SerializeField] private GameObject page5;
    [SerializeField] private GameObject page6;

    [Header("Navigation Buttons (inside pages)")]
    [SerializeField] private Button prevOnPage1; // close book (show cover)
    [SerializeField] private Button nextOnPage2; // -> 3/4
    [SerializeField] private Button prevOnPage3; // -> 1/2
    [SerializeField] private Button nextOnPage4; // -> 5/6
    [SerializeField] private Button prevOnPage5; // -> 3/4
    [SerializeField] private Button nextOnPage6; // close book (show cover)

    [Header("Flip Overlay (optional, for page flip feel)")]
    [SerializeField] private RectTransform flipOverlayRect;
    [SerializeField] private GameObject flipOverlayGO;

    [Header("Motion Settings")]
    [Tooltip("How much the FRONT COVER slides right before turning")]
    [SerializeField] private float coverSlideX = 40f;

    [Tooltip("Cover turn angle (180 = full flip). Use +180 or -180 depending on pivot direction.")]
    [SerializeField] private float coverTurnAngleY = -180f;

    [SerializeField] private float coverOpenDuration = 0.22f;
    [SerializeField] private float coverCloseDuration = 0.18f;

    [SerializeField] private float pageFlipDuration = 0.18f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip flipClip;

    // State
    private bool journalVisible; // cover visible (Tab toggles this)
    private bool bookOpen;
    private bool busy;
    private Spread currentSpread = Spread.OneTwo;

    private Vector2 coverStartPos;
    private Quaternion coverClosedRot;
    private Quaternion coverOpenRot;

    private Coroutine routine;

    private void Awake()
    {
        // Auto-find button if not assigned
        if (frontCoverButton == null && frontCoverGO != null)
            frontCoverButton = frontCoverGO.GetComponent<Button>();

        if (frontCoverRect == null && frontCoverGO != null)
            frontCoverRect = frontCoverGO.GetComponent<RectTransform>();

        // Setup starting transforms
        coverStartPos = frontCoverRect.anchoredPosition;
        coverClosedRot = Quaternion.identity;
        coverOpenRot = Quaternion.Euler(0f, coverTurnAngleY, 0f);

        // Start: Journal root ON, but everything inside OFF (your rule)
        HideAllInstant();

        // Wire cover click
        if (frontCoverButton != null)
        {
            frontCoverButton.onClick.RemoveListener(OnCoverClicked);
            frontCoverButton.onClick.AddListener(OnCoverClicked);
        }

        // Wire nav buttons
        Hook(prevOnPage1, CloseBookToCover);
        Hook(nextOnPage2, () => FlipTo(Spread.ThreeFour));
        Hook(prevOnPage3, () => FlipTo(Spread.OneTwo));
        Hook(nextOnPage4, () => FlipTo(Spread.FiveSix));
        Hook(prevOnPage5, () => FlipTo(Spread.ThreeFour));
        Hook(nextOnPage6, CloseBookToCover);

        if (menuCursorGO) menuCursorGO.SetActive(false);
        if (flipOverlayGO) flipOverlayGO.SetActive(false);
    }

    private void Update()
    {
        if (busy) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (!journalVisible)
            {
                ShowCoverOnlyInstant();
                LockPlayer(true);
            }
            else
            {
                // put away journal
                if (bookOpen)
                    StartExclusive(CloseThenHideAll());
                else
                {
                    HideAllInstant();
                    LockPlayer(false);
                }
            }
        }
    }

    // =========================
    // COVER CLICK
    // =========================
    public void OnCoverClicked()
    {
        if (busy) return;
        if (!journalVisible) return;
        if (bookOpen) return;

        StartExclusive(OpenBookFromCover());
    }

    // =========================
    // OPEN / CLOSE
    // =========================

    // OPEN: Slide FRONT COVER slightly right -> rotate 180 -> then show OpenBook + pages
    private IEnumerator OpenBookFromCover()
    {
        busy = true;
        Play(openClip);

        // Ensure cover is visible, openbook is hidden until flip completes
        frontCoverGO.SetActive(true);
        openBookGO.SetActive(false);
        if (flipOverlayGO) flipOverlayGO.SetActive(false);

        // Reset cover pose
        frontCoverRect.anchoredPosition = coverStartPos;
        frontCoverRect.localRotation = coverClosedRot;

        Vector2 slidPos = coverStartPos + new Vector2(coverSlideX, 0f);

        // Phase A: slide slightly right
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverOpenDuration * 0.35f);
            float s = EaseOutCubic(t);
            frontCoverRect.anchoredPosition = Vector2.LerpUnclamped(coverStartPos, slidPos, s);
            yield return null;
        }

        // Phase B: rotate 180 like a door
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverOpenDuration * 0.65f);
            float s = EaseInOutCubic(t);
            frontCoverRect.localRotation = Quaternion.SlerpUnclamped(coverClosedRot, coverOpenRot, s);
            yield return null;
        }

        // NOW: book is open -> show OpenBook and pages
        openBookGO.SetActive(true);
        if (backPagesGO) backPagesGO.SetActive(true);
        if (creaseGO) creaseGO.SetActive(true);

        ShowSpread(Spread.OneTwo);

        // Hide the cover after it finishes turning (so you don't see it behind)
        frontCoverGO.SetActive(false);

        bookOpen = true;
        busy = false;
    }

    private void CloseBookToCover()
    {
        if (busy) return;
        if (!bookOpen) return;

        StartExclusive(CloseBookToCoverRoutine());
    }

    // CLOSE: hide pages -> show cover (already at 180) -> rotate back -> slide back -> hide OpenBook
    private IEnumerator CloseBookToCoverRoutine()
    {
        busy = true;
        Play(closeClip);

        // Hide pages + openbook bits first
        SetAllPagesOff();
        if (flipOverlayGO) flipOverlayGO.SetActive(false);

        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);

        openBookGO.SetActive(false);

        // Show cover in "open" pose so it can close back
        frontCoverGO.SetActive(true);
        frontCoverRect.anchoredPosition = coverStartPos + new Vector2(coverSlideX, 0f);
        frontCoverRect.localRotation = coverOpenRot;

        Vector2 slidPos = coverStartPos + new Vector2(coverSlideX, 0f);

        // Phase A: rotate back to closed
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverCloseDuration * 0.65f);
            float s = EaseInOutCubic(t);
            frontCoverRect.localRotation = Quaternion.SlerpUnclamped(coverOpenRot, coverClosedRot, s);
            yield return null;
        }

        // Phase B: slide back to original position
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverCloseDuration * 0.35f);
            float s = EaseInCubic(t);
            frontCoverRect.anchoredPosition = Vector2.LerpUnclamped(slidPos, coverStartPos, s);
            yield return null;
        }

        frontCoverRect.anchoredPosition = coverStartPos;
        frontCoverRect.localRotation = coverClosedRot;

        bookOpen = false;
        currentSpread = Spread.OneTwo;
        busy = false;
    }

    private IEnumerator CloseThenHideAll()
    {
        yield return CloseBookToCoverRoutine();
        HideAllInstant();
        LockPlayer(false);
    }

    // =========================
    // PAGE FLIP
    // =========================
    private void FlipTo(Spread target)
    {
        if (busy) return;
        if (!bookOpen) return;

        StartExclusive(FlipToRoutine(target));
    }

    private IEnumerator FlipToRoutine(Spread target)
    {
        if (target == currentSpread) yield break;

        busy = true;
        Play(flipClip);

        // Optional overlay on
        if (flipOverlayGO && flipOverlayRect)
        {
            flipOverlayGO.SetActive(true);
            flipOverlayRect.localRotation = Quaternion.identity;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, pageFlipDuration);
            float s = EaseInOutCubic(t);

            if (flipOverlayRect)
            {
                float angle = Mathf.Lerp(0f, -180f, s);
                flipOverlayRect.localRotation = Quaternion.Euler(0f, angle, 0f);
            }

            // Swap pages at midpoint
            if (t >= 0.5f && currentSpread != target)
                ShowSpread(target);

            yield return null;
        }

        if (flipOverlayGO) flipOverlayGO.SetActive(false);

        busy = false;
    }

    // =========================
    // SPREAD VISIBILITY
    // =========================
    private void ShowSpread(Spread spread)
    {
        SetAllPagesOff();
        currentSpread = spread;

        switch (spread)
        {
            case Spread.OneTwo:
                if (page1) page1.SetActive(true);
                if (page2) page2.SetActive(true);
                break;

            case Spread.ThreeFour:
                if (page3) page3.SetActive(true);
                if (page4) page4.SetActive(true);
                break;

            case Spread.FiveSix:
                if (page5) page5.SetActive(true);
                if (page6) page6.SetActive(true);
                break;
        }
    }

    private void SetAllPagesOff()
    {
        if (page1) page1.SetActive(false);
        if (page2) page2.SetActive(false);
        if (page3) page3.SetActive(false);
        if (page4) page4.SetActive(false);
        if (page5) page5.SetActive(false);
        if (page6) page6.SetActive(false);
    }

    // =========================
    // TAB SHOW/HIDE (Journal root stays ON)
    // =========================
    private void ShowCoverOnlyInstant()
    {
        journalVisible = true;

        // show only cover
        frontCoverGO.SetActive(true);
        frontCoverRect.anchoredPosition = coverStartPos;
        frontCoverRect.localRotation = coverClosedRot;

        // hide open book
        openBookGO.SetActive(false);
        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);
        SetAllPagesOff();

        if (flipOverlayGO) flipOverlayGO.SetActive(false);

        bookOpen = false;
        currentSpread = Spread.OneTwo;
    }

    private void HideAllInstant()
    {
        journalVisible = false;

        // hide everything inside
        if (frontCoverGO) frontCoverGO.SetActive(false);
        if (openBookGO) openBookGO.SetActive(false);

        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);

        SetAllPagesOff();

        if (flipOverlayGO) flipOverlayGO.SetActive(false);

        bookOpen = false;
        busy = false;
        currentSpread = Spread.OneTwo;

        // Reset cover pose for next open
        if (frontCoverRect)
        {
            frontCoverRect.anchoredPosition = coverStartPos;
            frontCoverRect.localRotation = coverClosedRot;
        }
    }

    // =========================
    // PLAYER LOCK + MENU CURSOR
    // =========================
    private void LockPlayer(bool locked)
    {
        if (playerScriptsToDisable != null)
            foreach (var s in playerScriptsToDisable)
                if (s) s.enabled = !locked;

        if (lookScriptsToDisable != null)
            foreach (var s in lookScriptsToDisable)
                if (s) s.enabled = !locked;

        if (playerRigidbody && locked)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (menuCursorGO) menuCursorGO.SetActive(locked);

        if (!locked)
        {
            UnityEngine.Cursor.visible = false;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        }

        if (freezeTime)
            Time.timeScale = locked ? 0f : 1f;
    }

    // =========================
    // ROUTINE MANAGEMENT
    // =========================
    private void StartExclusive(IEnumerator r)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(r);
    }

    // =========================
    // HELPERS
    // =========================
    private void Hook(Button b, System.Action action)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => { if (!busy) action(); });
    }

    private void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}

