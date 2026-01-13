using UnityEngine;
using System.Reflection;

[RequireComponent(typeof(Door))]
public class DoorLock : MonoBehaviour
{
    [Header("Lock State")]
    [Tooltip("If true, door starts locked.")]
    public bool startLocked = true;

    [Tooltip("If true, door locks automatically during hunts (front door behavior).")]
    public bool lockDuringHunt = false;

    [Header("Key Door Settings")]
    [Tooltip("If empty, this door does NOT require a key (but can still be hunt-locked).")]
    public string requiredKeyId = "";

    [Header("Sounds")]
    public AudioSource audioSource;
    public AudioClip unlockSound;
    [Range(0f, 1f)] public float unlockVolume = 0.9f;

    [Header("Locked Attempt Sound")]
    [Tooltip("Played when the player tries to open a locked door.")]
    public AudioClip lockedSound;
    [Range(0f, 1f)] public float lockedVolume = 0.9f;

    [Header("Hunt Door Sounds (Optional)")]
    public AudioClip huntLockSound;
    [Range(0f, 1f)] public float huntLockVolume = 0.9f;

    private bool isLocked;
    private Door door;

    // if hunt is controlling this lock right now
    private bool huntLockedActive = false;

    // cache Door private "isOpen" (so we can close if needed)
    private static FieldInfo _doorIsOpenField;

    void Awake()
    {
        door = GetComponent<Door>();
        isLocked = startLocked;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        CacheDoorReflection();
    }

    void OnEnable()
    {
        GhostPursuit.OnHuntStateChanged += HandleHuntStateChanged;
    }

    void OnDisable()
    {
        GhostPursuit.OnHuntStateChanged -= HandleHuntStateChanged;
    }

    public bool IsLocked() => isLocked;

    // Called by Interaction.cs when pressing E on the door
    public bool TryOpenOrToggle()
    {
        if (isLocked)
        {
            // 🔊 Play locked sound
            if (audioSource != null && lockedSound != null)
                audioSource.PlayOneShot(lockedSound, lockedVolume);

            return false;
        }

        if (door != null)
            door.Toggle();

        return true;
    }

    public bool RequiresKey()
    {
        return !string.IsNullOrEmpty(requiredKeyId);
    }

    public bool PlayerHasValidKeyInHand(PlayerInventory inventory)
    {
        if (!RequiresKey())
            return false;

        if (inventory == null)
            return false;

        InventoryItem item = inventory.GetCurrentItem();
        if (item == null)
            return false;

        KeyItem key = item.GetComponent<KeyItem>();
        if (key == null)
            return false;

        return key.keyId == requiredKeyId;
    }

    // Called by Interaction.cs on LEFT CLICK when aiming at locked door with key
    public bool TryUnlockWithKey(PlayerInventory inventory)
    {
        // 🔒 Hunt-locked doors can NEVER be unlocked by key
        if (huntLockedActive)
        {
            if (audioSource != null && lockedSound != null)
                audioSource.PlayOneShot(lockedSound, lockedVolume);
            return false;
        }

        if (!isLocked)
            return false;

        if (!PlayerHasValidKeyInHand(inventory))
        {
            if (audioSource != null && lockedSound != null)
                audioSource.PlayOneShot(lockedSound, lockedVolume);
            return false;
        }

        isLocked = false;

        if (audioSource != null && unlockSound != null)
            audioSource.PlayOneShot(unlockSound, unlockVolume);

        return true;
    }

    // Manual lock controls
    public void Lock()
    {
        isLocked = true;
    }

    public void Unlock()
    {
        if (huntLockedActive) return;
        isLocked = false;
    }

    // ------------------------
    // HUNT HANDLING
    // ------------------------
    void HandleHuntStateChanged(bool hunting)
    {
        if (!lockDuringHunt)
            return;

        if (hunting)
        {
            ForceCloseIfOpen();
            isLocked = true;
            huntLockedActive = true;

            if (audioSource != null && huntLockSound != null)
                audioSource.PlayOneShot(huntLockSound, huntLockVolume);
        }
        else
        {
            huntLockedActive = false;
            isLocked = startLocked;
        }
    }

    void ForceCloseIfOpen()
    {
        if (door == null) return;

        if (TryGetDoorIsOpen(door, out bool isOpen) && isOpen)
            door.Toggle();
    }

    static void CacheDoorReflection()
    {
        if (_doorIsOpenField != null) return;
        _doorIsOpenField = typeof(Door).GetField("isOpen", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    static bool TryGetDoorIsOpen(Door d, out bool isOpen)
    {
        isOpen = false;
        if (d == null) return false;

        CacheDoorReflection();
        if (_doorIsOpenField == null) return false;

        object val = _doorIsOpenField.GetValue(d);
        if (val is bool b)
        {
            isOpen = b;
            return true;
        }

        return false;
    }
}
