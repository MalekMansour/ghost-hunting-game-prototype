using UnityEngine;
using System;

public class DoorLock : MonoBehaviour, IInteractable
{
    public enum LockMode
    {
        KeyDoor,        // locked/unlocked by key (player can toggle with key in hand)
        HuntControlled  // locks during hunt only (player cannot unlock)
    }

    [Header("Mode")]
    public LockMode mode = LockMode.KeyDoor;

    [Header("Start State")]
    public bool startsLocked = true;

    [Header("Key Settings (KeyDoor)")]
    [Tooltip("If empty, any key works. Otherwise must match DoorKey.keyId on the held item.")]
    public string requiredKeyId = "";

    [Tooltip("If true, player can lock/unlock the door with the key in hand (left click).")]
    public bool allowKeyToggle = true;

    [Header("Hunt Settings (HuntControlled)")]
    [Tooltip("If true, this door automatically locks when hunting and unlocks when hunt ends.")]
    public bool lockWhenHunting = true;

    [Tooltip("If true, this door starts unlocked even if startsLocked is true (useful for main doors).")]
    public bool forceStartUnlockedForHuntDoors = true;

    [Header("References")]
    [Tooltip("Door script that actually opens/closes. If null, will auto-find on this object or parent.")]
    public Door door;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip lockedSound;
    [Range(0f, 1f)] public float lockedVolume = 0.8f;

    private bool isLocked;

    void Awake()
    {
        if (door == null) door = GetComponentInParent<Door>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Initialize lock state
        if (mode == LockMode.HuntControlled && forceStartUnlockedForHuntDoors)
            isLocked = false;
        else
            isLocked = startsLocked;

        ApplyDoorState();
        HuntState.OnHuntChanged += OnHuntChanged;
    }

    void OnDestroy()
    {
        HuntState.OnHuntChanged -= OnHuntChanged;
    }

    void OnHuntChanged(bool hunting)
    {
        if (mode != LockMode.HuntControlled) return;
        if (!lockWhenHunting) return;

        isLocked = hunting;
        ApplyDoorState();
    }

    void ApplyDoorState()
    {
        // We do NOT disable this script or other scripts.
        // We only gate the door toggle through Interaction + DoorLock checks.
        // But it's also useful to prevent accidental toggles via other calls:
        if (door != null)
        {
            // Optional: if your Door script has something like "canOpen" you could set it here.
            // Since we don't know your Door implementation, we just keep state in DoorLock
            // and Interaction will check it before calling door.Toggle().
        }
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    // Called from Interaction when player presses E on door
    public bool TryOpenOrToggle()
    {
        if (door == null) return false;

        if (isLocked)
        {
            PlayLockedSound();
            return true; // door was "handled" (we hit it), but didn't open
        }

        door.Toggle();
        return true;
    }

    // Called from Interaction (LEFT CLICK) for Interactable layer objects
    public void Interact()
    {
        // Only KeyDoor can be toggled by player via interact
        if (mode != LockMode.KeyDoor) { PlayLockedSound(); return; }
        if (!allowKeyToggle) { PlayLockedSound(); return; }

        // Must have correct key in hand
        PlayerInventory inv = FindLocalPlayerInventory();
        if (inv == null) { PlayLockedSound(); return; }

        InventoryItem held = inv.GetCurrentItem();
        if (!HasValidKey(held))
        {
            PlayLockedSound();
            return;
        }

        // Toggle lock state (lock/unlock)
        isLocked = !isLocked;
        ApplyDoorState();
    }

    // Used by Interaction crosshair logic
    public bool PlayerHasValidKeyInHand(PlayerInventory inv)
    {
        if (inv == null) return false;
        return HasValidKey(inv.GetCurrentItem());
    }

    bool HasValidKey(InventoryItem heldItem)
    {
        if (heldItem == null) return false;

        DoorKey key = heldItem.GetComponent<DoorKey>();
        if (key == null) return false;

        if (string.IsNullOrEmpty(requiredKeyId)) return true;
        return key.keyId == requiredKeyId;
    }

    void PlayLockedSound()
    {
        if (audioSource == null || lockedSound == null) return;
        audioSource.PlayOneShot(lockedSound, lockedVolume);
    }

    // NOTE: This assumes single-player or "local player".
    // If you have multiplayer, you should pass the player's inventory into Interact().
    PlayerInventory FindLocalPlayerInventory()
    {
        // Most of your scripts are on the player object. If this is single player, this is fine:
        return FindObjectOfType<PlayerInventory>();
    }

    // ---------------------------
    // HUNT STATE BRIDGE (simple)
    // ---------------------------
    // Any script can call: HuntState.SetHunting(true/false)
    public static class HuntState
    {
        public static bool IsHunting { get; private set; }
        public static event Action<bool> OnHuntChanged;

        public static void SetHunting(bool hunting)
        {
            if (IsHunting == hunting) return;
            IsHunting = hunting;
            OnHuntChanged?.Invoke(IsHunting);
        }
    }
}

// Put this on your key prefab item (the thing you hold in hand)
public class DoorKey : MonoBehaviour
{
    public string keyId = "ShortcutKey";
}

