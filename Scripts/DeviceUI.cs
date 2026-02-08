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

    [Header("Ghost Buttons (drag your ghost buttons here in order)")]
    public Button[] ghostButtons;

    [Header("Circles (optional)")]
    [Tooltip("If you leave this empty, we will auto-find a circle child under each ghost button.")]
    public GameObject[] circleHighlights;

    [Tooltip("If auto-finding circles, we will prefer a child whose name contains this (case-insensitive).")]
    public string circleChildNameContains = "rectangle"; 

    [Tooltip("If true, and circleHighlights is empty, we auto-find circle children under each ghost button.")]
    public bool autoFindCircles = true;

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

    [Header("Behavior")]
    public bool autoDisableSubmitIfNotAllMatch = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool isOpen;
    private float cachedTimeScale = 1f;

    private void Awake()
    {
        if (rootPanel == null) rootPanel = gameObject;

        // Wire ghost buttons
        if (ghostButtons != null)
        {
            for (int i = 0; i < ghostButtons.Length; i++)
            {
                int idx = i;
                if (ghostButtons[i] != null)
                {
                    ghostButtons[i].onClick.RemoveAllListeners();
                    ghostButtons[i].onClick.AddListener(() => OnGhostPressed(idx));
                }
            }
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmitPressed);
        }

        // Auto-find circles if not provided
        if (autoFindCircles && (circleHighlights == null || circleHighlights.Length == 0))
        {
            if (ghostButtons != null)
            {
                circleHighlights = new GameObject[ghostButtons.Length];

                for (int i = 0; i < ghostButtons.Length; i++)
                {
                    if (ghostButtons[i] == null) continue;

                    Transform circle = FindCircleChild(ghostButtons[i].transform);
                    if (circle != null)
                    {
                        circleHighlights[i] = circle.gameObject;
                        circleHighlights[i].SetActive(false); // start OFF
                    }
                    else if (debugLogs)
                    {
                        Debug.LogWarning($"[DeviceUI] No circle child found under '{ghostButtons[i].name}'. " +
                                         $"Make sure the circle is a CHILD of the button.", ghostButtons[i]);
                    }
                }
            }
        }

        // Start hidden (panel only)
        rootPanel.SetActive(false);
        SetStatus("");
        UpdateCircles(-1);

        if (menuCursorGO) menuCursorGO.SetActive(false);

        // Ensure game starts in FPS mode
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        if (autoDisableSubmitIfNotAllMatch && submitButton != null)
            submitButton.interactable = AreAllPlayersMatchingSelection();
    }

    public void Open()
    {
        if (isOpen) return;

        // Ensure this script stays active so Update keeps running for ESC
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Show panel FIRST (prevents "locked with nothing visible")
        if (rootPanel == null) rootPanel = gameObject;
        rootPanel.SetActive(true);

        isOpen = true;

        // Lock AFTER panel is visible
        PrepareLocalPlayerRefsIfMissing();
        LockLocalPlayer(true);

        SetStatus("Select the ghost.");

        GhostVoteNetcode vote = GetLocalVote();
        int current = (vote != null) ? vote.SelectedGhostIndex.Value : -1;
        UpdateCircles(current);

        // Optional: if no selection yet, auto-select first ghost on open
        if (current < 0 && ghostButtons != null && ghostButtons.Length > 0)
        {
            OnGhostPressed(0);
        }

        if (debugLogs) Debug.Log("[DeviceUI] Open()", this);
    }

    public void Close()
    {
        // Always restore cursor + controls, even if state is weird
        isOpen = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);

        SetStatus("");

        // Restore player control first
        LockLocalPlayer(false);

        // HARD cursor reset (failsafe)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (menuCursorGO)
            menuCursorGO.SetActive(false);

        if (debugLogs) Debug.Log("[DeviceUI] Close() -> cursor hidden & locked", this);
    }

    // Absolute failsafe: if this object is disabled while open or during scene swaps
    private void OnDisable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (menuCursorGO)
            menuCursorGO.SetActive(false);

        LockLocalPlayer(false);
        isOpen = false;
    }

    private void OnGhostPressed(int index)
    {
        if (!isOpen) return;

        GhostVoteNetcode vote = GetLocalVote();
        if (vote == null)
        {
            SetStatus("No local player vote found.");
            return;
        }

        // Circle locally immediately
        UpdateCircles(index);

        // Status message (optional)
        string label = (ghostButtons != null && index >= 0 && index < ghostButtons.Length && ghostButtons[index] != null)
            ? ghostButtons[index].name
            : index.ToString();
        SetStatus($"Selected: {label}");

        // Tell server: this becomes the "selected ghost" for submit
        vote.SetSelectedGhostServerRpc(index);
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

    private void UpdateCircles(int selectedIndex)
    {
        if (circleHighlights == null) return;

        for (int i = 0; i < circleHighlights.Length; i++)
        {
            if (circleHighlights[i] == null) continue;
            circleHighlights[i].SetActive(i == selectedIndex);
        }
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

    private GhostVoteNetcode GetLocalVote()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return null;

        NetworkObject localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return null;

        return localPlayer.GetComponent<GhostVoteNetcode>();
    }

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

        if (debugLogs)
        {
            Debug.Log($"[DeviceUI] Auto-found local refs: move={Len(playerScriptsToDisable)} look={Len(lookScriptsToDisable)} steps={Len(footstepScriptsToDisable)} rb={(playerRigidbody ? playerRigidbody.name : "none")}", localPlayer);
        }
    }

    private int Len(Object[] arr) => arr == null ? 0 : arr.Length;

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

        if (menuCursorGO) menuCursorGO.SetActive(locked);

        if (locked)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
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

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    // -------------------------
    // Circle auto-find helper
    // -------------------------
    private Transform FindCircleChild(Transform buttonRoot)
    {
        if (buttonRoot == null) return null;

        // Search ALL children (including inactive)
        Transform[] all = buttonRoot.GetComponentsInChildren<Transform>(true);

        // Prefer name match
        if (!string.IsNullOrEmpty(circleChildNameContains))
        {
            string needle = circleChildNameContains.ToLower();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].name.ToLower().Contains(needle))
                    return all[i];
            }
        }

        // Fallback: first child with an Image (often your circle)
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i] == buttonRoot) continue;

            if (all[i].GetComponent<Image>() != null)
                return all[i];
        }

        return null;
    }
}
