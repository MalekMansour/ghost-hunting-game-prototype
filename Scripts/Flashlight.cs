using UnityEngine;
using System.Collections.Generic;

public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public float holdTime = 2f;

    private float holdTimer = 0f;
    private bool flashlightOn = false;

    private Transform flashlightRoot;
    private List<GameObject> flashlightChildren = new List<GameObject>();

    void Start()
    {
        FindFlashlightObject();
        SetFlashlight(false); // force OFF at start
    }

    void Update()
    {
        if (Input.GetKey(toggleKey))
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                ToggleFlashlight();
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    void FindFlashlightObject()
    {
        flashlightRoot = FindChildRecursive(transform.root, "Flashlight");

        if (flashlightRoot == null)
        {
            Debug.LogError("❌ Flashlight object NOT found anywhere under PLAYER ROOT!");
            return;
        }

        flashlightChildren.Clear();

        foreach (Transform child in flashlightRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child != flashlightRoot)
                flashlightChildren.Add(child.gameObject);
        }

        Debug.Log($"✅ Flashlight found with {flashlightChildren.Count} child object(s).");
    }

    void ToggleFlashlight()
    {
        flashlightOn = !flashlightOn;
        SetFlashlight(flashlightOn);
    }

    void SetFlashlight(bool state)
    {
        foreach (GameObject obj in flashlightChildren)
            obj.SetActive(state);
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
