using UnityEngine;

public class ToggleCanvas : MonoBehaviour
{
    public GameObject canvasToToggle;

    public void Toggle()
    {
        if (canvasToToggle == null) return;

        canvasToToggle.SetActive(!canvasToToggle.activeSelf);
    }
}

