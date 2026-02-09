using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class DeviceUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject rootPanel;
    public Button submitButton;
    public TMP_Text statusText;

    [Header("Ghost Buttons (optional: can auto-find)")]
    public Button[] ghostButtons;

    [Header("Circles (optional: can auto-find)")]
    public GameObject[] circleHighlights;

    public string circleChildNameContains = "rectangle";
    public bool autoFindCircles = true;
    public bool autoFindGhostButtons = true;

    [Header("Close")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Menu Cursor (optional custom cursor GO)")]
    public GameObject menuCursorGO;

    [Header("Player Lock While Device Open (optional manual assign)")]
    public MonoBehaviour[] playerScriptsToDisable;
    public MonoBehaviour[] lookScriptsToDisable;
    public MonoBehaviour[] footstepScriptsToDisable;
    public Rigidbody playerRigidbody;
    public bool freezeTime = false;

    [Header("Auto-Find Script Names (if arrays above are empty)")]
    public string[] movementScriptNames = new[] { "PlayerMovement" };
    public string[] lookScriptNames = new[] { "PlayerView" };
    public string[] footstepScriptNames = new[] { "Footsteps" };

    [Header("SFX")]
    public AudioSource uiAudioSource;
    public AudioClip openDeviceClip;
    public AudioClip ghostClickClip;
    [Range(0f, 1f)] public float uiSfxVolume = 1f;

    [Header("Behavior")]
    public bool autoDisableSubmitIfNotAllMatch = true;

    [Header("Cursor Override")]
    [Tooltip("How many frames after closing we HARD force cursor hidden+locked in LateUpdate.")]
    public int forceCloseCursorFrames = 180; // ~3 seconds @60fps (strong)
    private int closeCursorFramesLeft = 0;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool isOpen;
    private float cachedTimeScale = 1f;

    private void Awake()
    {
        if (rootPanel == null) rootPanel = gameObject;

        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
                uiAudioSource = GetComponentInChildren<AudioSource>(true);
        }

        rootPanel.SetActive(false);
        SetStatus("");
        SafeSetMenuCursor(false);
        ForceFPSCursor();

        if (autoFindGhostButtons && (ghostButtons == null || ghostButtons.Length == 0))
            ghostButtons = AutoFindGhostButtonsUnderPanel();

        if (ghostButtons != null)
        {
            for (int i = 0; i < ghostButtons.Length; i++)
            {
                int idx = i;
                if (ghostButtons[i] != null)
                    ghostButtons[i].onClick.AddListener(() => OnGhostPressed(idx));
            }
        }

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitPressed);

        if (autoFindCircles && (circleHighlights == null || circleHighlights.Length == 0))
            circleHighlights = AutoFindCirclesForButtons(ghostButtons);

        UpdateCircles(-1);
    }

    private void Update()
    {
        // If open, let ESC close
        if (isOpen && Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        // Fail-safe: if something hid the panel while open, close cleanly
        if (isOpen && (rootPanel == null || !rootPanel.activeInHierarchy))
        {
            ForceClose("Panel hidden externally");
            return;
        }

        if (!isOpen) return;

        // While open: cursor ON + unlocked (Update)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SafeSetMenuCursor(true);

        if (autoDisableSubmitIfNotAllMatch && submitButton != null)
            submitButton.interactable = AreAllPlayersMatchingSelection();
    }

    private void LateUpdate()
    {
        // ✅ THE FIX:
        // If closed (or just closed), force cursor OFF at the end of the frame,
        // so any other script that turned it ON earlier loses.
        if (!isOpen && closeCursorFramesLeft > 0)
        {
            closeCursorFramesLeft--;
            ForceFPSCursor();
            SafeSetMenuCursor(false);
        }
    }

    public void Open()
    {
        if (isOpen) return;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (rootPanel == null) rootPanel = gameObject;

        rootPanel.SetActive(true);
        isOpen = true;

        // Stop forcing cursor closed
        closeCursorFramesLeft = 0;

        PrepareLocalPlayerRefsIfMissing();
        LockLocalPlayer(true);

        SetStatus("Select the ghost.");
        PlayUISfx(openDeviceClip);

        GhostVoteNetcode vote = GetLocalVote();
        int current = (vote != null) ? vote.SelectedGhostIndex.Value : -1;
        UpdateCircles(current);

        // Force cursor ON immediately
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SafeSetMenuCursor(true);

        if (debugLogs) Debug.Log("[DeviceUI] Open()", this);
    }

    public void Close()
    {
        ForceClose("Close()");
    }

    private void ForceClose(string reason)
    {
        if (debugLogs) Debug.Log($"[DeviceUI] ForceClose: {reason}", this);

        isOpen = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);

        SetStatus("");

        LockLocalPlayer(false);

        // ✅ Start hard forcing cursor OFF after close (LateUpdate)
        closeCursorFramesLeft = Mathf.Max(1, forceCloseCursorFrames);

        // Also do it immediately
        ForceFPSCursor();
        SafeSetMenuCursor(false);
    }

    private void OnDisable()
    {
        if (isOpen) LockLocalPlayer(false);
        isOpen = false;

        closeCursorFramesLeft = Mathf.Max(1, forceCloseCursorFrames);
        ForceFPSCursor();
        SafeSetMenuCursor(false);
    }

    private void OnDestroy()
    {
        ForceFPSCursor();
        SafeSetMenuCursor(false);

        if (isOpen) LockLocalPlayer(false);
        isOpen = false;
    }

    private void OnGhostPressed(int index)
    {
        if (!isOpen) return;

        PlayUISfx(ghostClickClip);

        UpdateCircles(index);

        GhostVoteNetcode vote = GetLocalVote();
        if (vote == null)
        {
            SetStatus("No local player vote found.");
            return;
        }

        vote.SetSelectedGhostServerRpc(index);

        string label =
            (ghostButtons != null && index >= 0 && index < ghostButtons.Length && ghostButtons[index] != null)
                ? ghostButtons[index].name
                : index.ToString();

        SetStatus($"Selected: {label}");
    }

    private void OnSubmitPressed()
    {
        if (!isOpen) return;

        GhostVoteNetcode vote = GetLocalVote();
        if (vote == null)
        {
            SetStatus("No local player vote found.");
            return;
        }

        if (!AreAllPlayersMatchingSelection())
        {
            SetStatus("Not everyone selected the same ghost.");
            return;
        }

        vote.SubmitGuessServerRpc();
    }

    public void OnSubmitResult(bool success, string message)
    {
        if (!isOpen) return;
        SetStatus(message);
    }

    private void PlayUISfx(AudioClip clip)
    {
        if (clip == null) return;

        if (uiAudioSource != null)
        {
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(uiSfxVolume));
            return;
        }

        if (Camera.main != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, Mathf.Clamp01(uiSfxVolume));
    }

    private void UpdateCircles(int selectedIndex)
    {
        if (circleHighlights == null || circleHighlights.Length == 0) return;

        for (int i = 0; i < circleHighlights.Length; i++)
        {
            if (circleHighlights[i] == null) continue;
            circleHighlights[i].SetActive(i == selectedIndex);
        }
    }

    // --------------------------
    // Auto-find helpers
    // --------------------------

    private Button[] AutoFindGhostButtonsUnderPanel()
    {
        if (rootPanel == null) return new Button[0];

        Button[] all = rootPanel.GetComponentsInChildren<Button>(true);
        if (all == null) return new Button[0];

        System.Collections.Generic.List<Button> list = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (submitButton != null && all[i] == submitButton) continue;
            list.Add(all[i]);
        }

        if (debugLogs) Debug.Log($"[DeviceUI] AutoFound ghost buttons: {list.Count}", this);
        return list.ToArray();
    }

    private GameObject[] AutoFindCirclesForButtons(Button[] buttons)
    {
        if (buttons == null) return new GameObject[0];

        GameObject[] circles = new GameObject[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            Transform circle = FindCircleChild(buttons[i].transform);
            if (circle != null)
            {
                circles[i] = circle.gameObject;
                circles[i].SetActive(false);
            }
        }

        return circles;
    }

    private Transform FindCircleChild(Transform buttonRoot)
    {
        if (buttonRoot == null) return null;

        Transform[] all = buttonRoot.GetComponentsInChildren<Transform>(true);
        string needle = string.IsNullOrEmpty(circleChildNameContains) ? "" : circleChildNameContains.ToLower();

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == buttonRoot) continue;

            if (!string.IsNullOrEmpty(needle) &&
                t.name.ToLower().Contains(needle) &&
                !t.gameObject.activeSelf)
            {
                return t;
            }
        }

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == buttonRoot) continue;

            if (!string.IsNullOrEmpty(needle) && t.name.ToLower().Contains(needle))
                return t;
        }

        Image buttonImage = buttonRoot.GetComponent<Image>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == buttonRoot) continue;

            Image img = t.GetComponent<Image>();
            if (img == null) continue;
            if (buttonImage != null && img == buttonImage) continue;

            if (!t.gameObject.activeSelf) return t;
        }

        return null;
    }

    // --------------------------
    // Netcode helpers
    // --------------------------

    private GhostVoteNetcode GetLocalVote()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return null;

        NetworkObject localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return null;

        return localPlayer.GetComponent<GhostVoteNetcode>();
    }

    private bool AreAllPlayersMatchingSelection()
    {
        GhostVoteNetcode[] all = FindObjectsByType<GhostVoteNetcode>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return false;

        int first = -9999;
        bool firstSet = false;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !all[i].IsSpawned) continue;

            int sel = all[i].SelectedGhostIndex.Value;
            if (sel < 0) return false;

            if (!firstSet)
            {
                first = sel;
                firstSet = true;
            }
            else if (sel != first)
            {
                return false;
            }
        }

        return firstSet;
    }

    // --------------------------
    // Player lock
    // --------------------------

    private void PrepareLocalPlayerRefsIfMissing()
    {
        bool needAny =
            (playerScriptsToDisable == null || playerScriptsToDisable.Length == 0) ||
            (lookScriptsToDisable == null || lookScriptsToDisable.Length == 0) ||
            (footstepScriptsToDisable == null || footstepScriptsToDisable.Length == 0) ||
            (playerRigidbody == null);

        if (!needAny) return;

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return;

        NetworkObject localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;

        Transform root = localPlayer.transform;

        if (playerScriptsToDisable == null || playerScriptsToDisable.Length == 0)
            playerScriptsToDisable = FindScriptsByNames(root, movementScriptNames);

        if (lookScriptsToDisable == null || lookScriptsToDisable.Length == 0)
            lookScriptsToDisable = FindScriptsByNames(root, lookScriptNames);

        if (footstepScriptsToDisable == null || footstepScriptsToDisable.Length == 0)
            footstepScriptsToDisable = FindScriptsByNames(root, footstepScriptNames);

        if (playerRigidbody == null)
            playerRigidbody = root.GetComponentInChildren<Rigidbody>(true);
    }

    private MonoBehaviour[] FindScriptsByNames(Transform root, string[] names)
    {
        if (root == null || names == null || names.Length == 0) return new MonoBehaviour[0];

        MonoBehaviour[] all = root.GetComponentsInChildren<MonoBehaviour>(true);
        System.Collections.Generic.List<MonoBehaviour> found = new System.Collections.Generic.List<MonoBehaviour>();

        for (int i = 0; i < all.Length; i++)
        {
            var m = all[i];
            if (m == null) continue;

            string tn = m.GetType().Name;
            for (int j = 0; j < names.Length; j++)
            {
                if (!string.IsNullOrEmpty(names[j]) && tn == names[j])
                {
                    found.Add(m);
                    break;
                }
            }
        }

        return found.ToArray();
    }

    private void LockLocalPlayer(bool locked)
    {
        if (playerScriptsToDisable != null)
            foreach (var s in playerScriptsToDisable)
                if (s) s.enabled = !locked;

        if (lookScriptsToDisable != null)
            foreach (var s in lookScriptsToDisable)
                if (s) s.enabled = !locked;

        if (footstepScriptsToDisable != null)
            foreach (var s in footstepScriptsToDisable)
                if (s) s.enabled = !locked;

        if (playerRigidbody && locked)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (freezeTime)
        {
            if (locked)
            {
                cachedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = cachedTimeScale;
            }
        }
    }

    private void ForceFPSCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SafeSetMenuCursor(bool on)
    {
        if (menuCursorGO) menuCursorGO.SetActive(on);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}
