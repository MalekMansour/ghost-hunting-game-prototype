using UnityEngine;

public class CloseCanvas : MonoBehaviour
{
    public GameObject canvasToClose;

    public void Close()
    {
        if (canvasToClose == null) return;

        canvasToClose.SetActive(false);
    }
}
