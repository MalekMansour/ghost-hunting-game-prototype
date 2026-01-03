using UnityEngine;

public class Cursor : MonoBehaviour
{
    [Header("Cursor Settings")]
    public Texture2D cursorTexture;

    [Tooltip("Cursor hotspot (usually center or top-left)")]
    public Vector2 hotspot = Vector2.zero;

    [Tooltip("Use this for UI menus")]
    public CursorMode cursorMode = CursorMode.Auto;

    void Start()
    {
        ApplyCursor();
    }

    void OnEnable()
    {
        ApplyCursor();
    }

    void ApplyCursor()
    {
        if (cursorTexture == null)
        {
            Debug.LogWarning("Cursor.cs: No cursor texture assigned.");
            return;
        }

        UnityEngine.Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
    }
}
