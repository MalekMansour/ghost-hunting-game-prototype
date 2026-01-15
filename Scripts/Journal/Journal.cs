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
    [SerializeField] private GameObject frontCoverGO;
    [SerializeField] private RectTransform frontCoverRect;
    [SerializeField] private Button frontCoverButton;

    [Header("Open Book Base")]
    [SerializeField] private GameObject openBookGO;
    [SerializeField] private GameObject backPagesGO;
    [SerializeField] private GameObject creaseGO;

    [Header("Pages Parent (optional)")]
    [SerializeField] private GameObject pagesRoot; // your empty "Pages" object (optional)

    [Header("Page Pivots (parents)")]
    [SerializeField] private GameObject page1Pivot;
    [SerializeField] private GameObject page2Pivot;
    [SerializeField] private GameObject page3Pivot;
    [SerializeField] private GameObject page4Pivot;
    [SerializeField] private GameObject page5Pivot;
    [SerializeField] private GameObject page6Pivot;

    [Header("Pages (actual visuals under each pivot)")]
    [SerializeField] private GameObject page1;
    [SerializeField] private GameObject page2;
    [SerializeField] private GameObject page3;
    [SerializeField] private GameObject page4;
    [SerializeField] private GameObject page5;
    [SerializeField] private GameObject page6;

    [Header("Navigation Buttons")]
    [SerializeField] private Button prevOnPage1; // close book (show cover)
    [SerializeField] private Button nextOnPage2; // -> 3/4
    [SerializeField] private Button prevOnPage3; // -> 1/2
    [SerializeField] private Button nextOnPage4; // -> 5/6
    [SerializeField] private Button prevOnPage5; // -> 3/4
    [SerializeField] private Button nextOnPage6; // close book (show cover)

    [Header("Motion Settings (Cover)")]
    [SerializeField] private float coverSlideX = 40f;
    [SerializeField] private float coverTurnAngleY = -180f;
    [SerializeField] private float coverOpenDuration = 0.22f;
    [SerializeField] private float coverCloseDuration = 0.18f;

    [Header("Page Flip Settings (Staged Reveal)")]
    [SerializeField] private float pageFlipDuration = 0.24f;

    [Tooltip("Forward: flips RIGHT page. Usually -180.")]
    [SerializeField] private float rightPageFlipAngleY = -180f;

    [Tooltip("Backward: flips LEFT page. Usually +180.")]
    [SerializeField] private float leftPageFlipAngleY = 180f;

    [Header("Audio (Guaranteed 2D)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioListener listener; // assign Main Camera's AudioListener (optional but recommended)
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip flipClip;

    // State
    private bool journalVisible;
    private bool bookOpen;
    private bool busy;
    private Spread currentSpread = Spread.OneTwo;

    private Vector2 coverStartPos;
    private Quaternion coverClosedRot;
    private Quaternion coverOpenRot;

    private Coroutine routine;

    private void Awake()
    {
        if (frontCoverButton == null && frontCoverGO != null)
            frontCoverButton = frontCoverGO.GetComponent<Button>();

        if (frontCoverRect == null && frontCoverGO != null)
            frontCoverRect = frontCoverGO.GetComponent<RectTransform>();

        coverStartPos = frontCoverRect.anchoredPosition;
        coverClosedRot = Quaternion.identity;
        coverOpenRot = Quaternion.Euler(0f, coverTurnAngleY, 0f);

        HideAllInstant();

        if (frontCoverButton != null)
        {
            frontCoverButton.onClick.RemoveListener(OnCoverClicked);
            frontCoverButton.onClick.AddListener(OnCoverClicked);
        }

        Hook(prevOnPage1, CloseBookToCover);
        Hook(nextOnPage2, () => FlipTo(Spread.ThreeFour));
        Hook(prevOnPage3, () => FlipTo(Spread.OneTwo));
        Hook(nextOnPage4, () => FlipTo(Spread.FiveSix));
        Hook(prevOnPage5, () => FlipTo(Spread.ThreeFour));
        Hook(nextOnPage6, CloseBookToCover);

        if (menuCursorGO) menuCursorGO.SetActive(false);
        ResetAllPivotRotations();
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
    private IEnumerator OpenBookFromCover()
    {
        busy = true;
        Play(openClip);

        frontCoverGO.SetActive(true);
        if (openBookGO) openBookGO.SetActive(false);

        frontCoverRect.anchoredPosition = coverStartPos;
        frontCoverRect.localRotation = coverClosedRot;

        Vector2 slidPos = coverStartPos + new Vector2(coverSlideX, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverOpenDuration * 0.35f);
            float s = EaseOutCubic(t);
            frontCoverRect.anchoredPosition = Vector2.LerpUnclamped(coverStartPos, slidPos, s);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverOpenDuration * 0.65f);
            float s = EaseInOutCubic(t);
            frontCoverRect.localRotation = Quaternion.SlerpUnclamped(coverClosedRot, coverOpenRot, s);
            yield return null;
        }

        if (openBookGO) openBookGO.SetActive(true);
        if (backPagesGO) backPagesGO.SetActive(true);
        if (creaseGO) creaseGO.SetActive(true);
        if (pagesRoot) pagesRoot.SetActive(true);

        ResetAllPivotRotations();
        ShowFullSpread(Spread.OneTwo);

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

    private IEnumerator CloseBookToCoverRoutine()
    {
        busy = true;
        Play(closeClip);

        SetAllPagesOff();
        if (pagesRoot) pagesRoot.SetActive(false);

        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);
        if (openBookGO) openBookGO.SetActive(false);

        frontCoverGO.SetActive(true);
        frontCoverRect.anchoredPosition = coverStartPos + new Vector2(coverSlideX, 0f);
        frontCoverRect.localRotation = coverOpenRot;

        Vector2 slidPos = coverStartPos + new Vector2(coverSlideX, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, coverCloseDuration * 0.65f);
            float s = EaseInOutCubic(t);
            frontCoverRect.localRotation = Quaternion.SlerpUnclamped(coverOpenRot, coverClosedRot, s);
            yield return null;
        }

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
        ResetAllPivotRotations();

        busy = false;
    }

    private IEnumerator CloseThenHideAll()
    {
        yield return CloseBookToCoverRoutine();
        HideAllInstant();
        LockPlayer(false);
    }

    // =========================
    // PAGE FLIP (YOUR EXACT REQUEST)
    // Forward: flip RIGHT page, show NEXT RIGHT immediately, LEFT updates at end
    // Backward: flip LEFT page, show PREV LEFT immediately, RIGHT updates at end
    // =========================
    private void FlipTo(Spread target)
    {
        if (busy) return;
        if (!bookOpen) return;

        StartExclusive(FlipRoutine_Staged(target));
    }

    private IEnumerator FlipRoutine_Staged(Spread target)
    {
        if (target == currentSpread) yield break;

        busy = true;
        Play(flipClip);

        bool forward = (int)target > (int)currentSpread;

        if (forward) yield return FlipForward(target);
        else yield return FlipBackward(target);

        busy = false;
    }

    private IEnumerator FlipForward(Spread target)
    {
        // current 1/2 -> target 3/4
        int curLeft = GetLeftPageIndex(currentSpread);
        int curRight = GetRightPageIndex(currentSpread);
        int tarLeft = GetLeftPageIndex(target);
        int tarRight = GetRightPageIndex(target);

        // Start state: show current spread
        SetAllPagesOff();
        SetPageActive(curLeft, true);
        SetPageActive(curRight, true);

        // IMMEDIATELY show target RIGHT under the flipping right page
        SetPageActive(tarRight, true);

        // Flip current right pivot
        RectTransform flipPivot = GetPivotRect(curRight);
        if (flipPivot == null)
        {
            // fallback hard switch
            SetAllPagesOff();
            SetPageActive(tarLeft, true);
            SetPageActive(tarRight, true);
            currentSpread = target;
            ResetAllPivotRotations();
            yield break;
        }

        flipPivot.localRotation = Quaternion.identity;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, pageFlipDuration);
            float s = EaseInOutCubic(t);

            float angle = Mathf.Lerp(0f, rightPageFlipAngleY, s); // usually -180
            flipPivot.localRotation = Quaternion.Euler(0f, angle, 0f);

            yield return null;
        }

        // End: NOW update left page (wait until animation finishes)
        flipPivot.localRotation = Quaternion.identity;

        SetAllPagesOff();
        SetPageActive(tarLeft, true);
        SetPageActive(tarRight, true);

        currentSpread = target;
        ResetAllPivotRotations();
    }

    private IEnumerator FlipBackward(Spread target)
    {
        // current 3/4 -> target 1/2
        int curLeft = GetLeftPageIndex(currentSpread);
        int curRight = GetRightPageIndex(currentSpread);
        int tarLeft = GetLeftPageIndex(target);
        int tarRight = GetRightPageIndex(target);

        // Start state: show current spread
        SetAllPagesOff();
        SetPageActive(curLeft, true);
        SetPageActive(curRight, true);

        // IMMEDIATELY show target LEFT under the flipping left page
        SetPageActive(tarLeft, true);

        // Flip current left pivot
        RectTransform flipPivot = GetPivotRect(curLeft);
        if (flipPivot == null)
        {
            // fallback hard switch
            SetAllPagesOff();
            SetPageActive(tarLeft, true);
            SetPageActive(tarRight, true);
            currentSpread = target;
            ResetAllPivotRotations();
            yield break;
        }

        flipPivot.localRotation = Quaternion.identity;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, pageFlipDuration);
            float s = EaseInOutCubic(t);

            float angle = Mathf.Lerp(0f, leftPageFlipAngleY, s); // usually +180
            flipPivot.localRotation = Quaternion.Euler(0f, angle, 0f);

            yield return null;
        }

        // End: NOW update right page (wait until animation finishes)
        flipPivot.localRotation = Quaternion.identity;

        SetAllPagesOff();
        SetPageActive(tarLeft, true);
        SetPageActive(tarRight, true);

        currentSpread = target;
        ResetAllPivotRotations();
    }

    // =========================
    // SHOW / HIDE
    // =========================
    private void ShowFullSpread(Spread spread)
    {
        SetAllPagesOff();
        currentSpread = spread;

        int left = GetLeftPageIndex(spread);
        int right = GetRightPageIndex(spread);

        SetPageActive(left, true);
        SetPageActive(right, true);
    }

    private void ShowCoverOnlyInstant()
    {
        journalVisible = true;

        frontCoverGO.SetActive(true);
        frontCoverRect.anchoredPosition = coverStartPos;
        frontCoverRect.localRotation = coverClosedRot;

        if (openBookGO) openBookGO.SetActive(false);
        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);
        if (pagesRoot) pagesRoot.SetActive(false);

        SetAllPagesOff();

        bookOpen = false;
        currentSpread = Spread.OneTwo;

        ResetAllPivotRotations();
    }

    private void HideAllInstant()
    {
        journalVisible = false;

        if (frontCoverGO) frontCoverGO.SetActive(false);
        if (openBookGO) openBookGO.SetActive(false);
        if (backPagesGO) backPagesGO.SetActive(false);
        if (creaseGO) creaseGO.SetActive(false);
        if (pagesRoot) pagesRoot.SetActive(false);

        SetAllPagesOff();

        bookOpen = false;
        busy = false;
        currentSpread = Spread.OneTwo;

        if (frontCoverRect)
        {
            frontCoverRect.anchoredPosition = coverStartPos;
            frontCoverRect.localRotation = coverClosedRot;
        }

        ResetAllPivotRotations();
    }

    // =========================
    // PAGES + PIVOTS
    // =========================
    private void SetAllPagesOff()
    {
        SetPageActive(1, false);
        SetPageActive(2, false);
        SetPageActive(3, false);
        SetPageActive(4, false);
        SetPageActive(5, false);
        SetPageActive(6, false);
    }

    private void SetPageActive(int pageNumber, bool on)
    {
        GameObject pivot = null;
        GameObject page = null;

        switch (pageNumber)
        {
            case 1: pivot = page1Pivot; page = page1; break;
            case 2: pivot = page2Pivot; page = page2; break;
            case 3: pivot = page3Pivot; page = page3; break;
            case 4: pivot = page4Pivot; page = page4; break;
            case 5: pivot = page5Pivot; page = page5; break;
            case 6: pivot = page6Pivot; page = page6; break;
        }

        if (pivot) pivot.SetActive(on);
        if (page) page.SetActive(on);
    }

    private RectTransform GetPivotRect(int pageNumber)
    {
        GameObject pivot = null;
        switch (pageNumber)
        {
            case 1: pivot = page1Pivot; break;
            case 2: pivot = page2Pivot; break;
            case 3: pivot = page3Pivot; break;
            case 4: pivot = page4Pivot; break;
            case 5: pivot = page5Pivot; break;
            case 6: pivot = page6Pivot; break;
        }
        return pivot ? pivot.GetComponent<RectTransform>() : null;
    }

    private int GetLeftPageIndex(Spread s) => s == Spread.OneTwo ? 1 : s == Spread.ThreeFour ? 3 : 5;
    private int GetRightPageIndex(Spread s) => s == Spread.OneTwo ? 2 : s == Spread.ThreeFour ? 4 : 6;

    private void ResetAllPivotRotations()
    {
        ResetPivot(page1Pivot);
        ResetPivot(page2Pivot);
        ResetPivot(page3Pivot);
        ResetPivot(page4Pivot);
        ResetPivot(page5Pivot);
        ResetPivot(page6Pivot);
    }

    private void ResetPivot(GameObject pivot)
    {
        if (!pivot) return;
        var rt = pivot.GetComponent<RectTransform>();
        if (rt) rt.localRotation = Quaternion.identity;
        else pivot.transform.localRotation = Quaternion.identity;
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
            playerRigidbody.velocity = Vector3.zero;
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
        if (!clip) return;

        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f; // FORCE 2D
            audioSource.volume = Mathf.Clamp01(sfxVolume);
            audioSource.PlayOneShot(clip);
            return;
        }

        if (listener != null)
        {
            AudioSource.PlayClipAtPoint(clip, listener.transform.position, Mathf.Clamp01(sfxVolume));
            return;
        }

        if (Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, Mathf.Clamp01(sfxVolume));
        }
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
