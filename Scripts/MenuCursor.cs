using UnityEngine;

public class MenuCursor : MonoBehaviour
{
    [Header("Menu Cursor Settings")]
    [SerializeField] private Texture2D cursorTexture;

    [Tooltip("Exact click point of the cursor (pixel coords from top-left)")]
    [SerializeField] private Vector2 hotspot = Vector2.zero; // (0,0) = sharp pointer tip

    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private void OnEnable()
    {
        ApplyCursor();
    }

    private void OnDisable()
    {
        ResetCursor();
    }

    private void ApplyCursor()
    {
        if (!cursorTexture)
        {
            Debug.LogWarning("MenuCursor: No cursor texture assigned.");
            return;
        }

        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, cursorMode);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
