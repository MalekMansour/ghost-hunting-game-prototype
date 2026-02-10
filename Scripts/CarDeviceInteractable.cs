using UnityEngine;

public class CarDeviceInteractable : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLogs = true;

    public void Interact()
    {
        OpenDevice("Interact()");
    }

    private void OnMouseDown()
    {
        OpenDevice("OnMouseDown()");
    }

    private void OpenDevice(string caller)
    {
        if (debugLogs) Debug.Log($"[CarDeviceInteractable] {caller} called on {name}", this);

        // Find DeviceUI even if the GameObject is inactive
        DeviceUI[] all = Resources.FindObjectsOfTypeAll<DeviceUI>();
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[CarDeviceInteractable] No DeviceUI found in scene (even inactive).", this);
            return;
        }

        DeviceUI ui = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            ui = all[i];
            break;
        }

        if (ui == null)
        {
            Debug.LogWarning("[CarDeviceInteractable] DeviceUI array existed but was all null.", this);
            return;
        }

        if (debugLogs) Debug.Log($"[CarDeviceInteractable] Opening DeviceUI on: {ui.gameObject.name}", ui);

        ui.Open();
    }
}
