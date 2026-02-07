using UnityEngine;

public class CarDeviceInteractable : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLogs = true;

    // Called by your interact system (left click)
    public void Interact()
    {
        OpenDevice("Interact()");
    }

    // Debug-only: clicking the car directly if it has a Collider
    private void OnMouseDown()
    {
        OpenDevice("OnMouseDown()");
    }

    private void OpenDevice(string caller)
    {
        if (debugLogs) Debug.Log($"[CarDeviceInteractable] {caller} called on {name}", this);

        // Find all DeviceUI scripts (including inactive)
        DeviceUI[] all = Resources.FindObjectsOfTypeAll<DeviceUI>();
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[CarDeviceInteractable] No DeviceUI found in scene (even inactive).", this);
            return;
        }

        // Prefer a DeviceUI that is in the scene (not prefab asset) and under a Canvas
        DeviceUI best = null;

        for (int i = 0; i < all.Length; i++)
        {
            var ui = all[i];
            if (ui == null) continue;

            // must be a scene object
            if (!ui.gameObject.scene.IsValid()) continue;

            // prefer ones under a Canvas
            if (ui.GetComponentInParent<Canvas>(true) == null) continue;

            best = ui;
            break;
        }

        // fallback: first scene-valid
        if (best == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                var ui = all[i];
                if (ui == null) continue;
                if (!ui.gameObject.scene.IsValid()) continue;
                best = ui;
                break;
            }
        }

        if (best == null)
        {
            Debug.LogWarning("[CarDeviceInteractable] Found DeviceUI but none were scene-valid.", this);
            return;
        }

        if (debugLogs) Debug.Log($"[CarDeviceInteractable] Opening DeviceUI on: {best.gameObject.name}", best);

        best.Open();
    }
}
