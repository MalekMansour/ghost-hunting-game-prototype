using UnityEngine;

public class ModelScaler : MonoBehaviour
{
    [Header("Visual Root (Mesh / Armature)")]
    public Transform visualRoot;

    [Header("Character Proportions")]
    public float uniformScale = 1f;
    public float feetYOffset = 0f;
    public void ApplyScale()
    {
        if (visualRoot == null)
        {
            Debug.LogError($"[{name}] VisualRoot not assigned!");
            return;
        }

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;

        visualRoot.localScale = Vector3.one * uniformScale;

        visualRoot.localPosition += Vector3.up * feetYOffset;
    }
}

