using UnityEngine;

public class ItemPosition : MonoBehaviour
{
    [Header("Hand Offsets (LIVE EDITABLE)")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    private Transform handParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalWorldScale;

    void Start()
    {
        originalWorldScale = transform.lossyScale;
    }

    void LateUpdate()
    {
        if (transform.parent == null)
        {
            handParent = null;
            return;
        }

        if (handParent == null &&
            (transform.parent.name.ToLower().Contains("hand") ||
             transform.parent.name.ToLower().Contains("holdpoint")))
        {
            handParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
        }

        if (handParent == null)
            return;

        // Apply position offset in world space
        transform.position = handParent.TransformPoint(originalLocalPosition + positionOffset);

        // Apply rotation offset
        transform.rotation = handParent.rotation * originalLocalRotation * Quaternion.Euler(rotationOffset);

        Vector3 parentScale = handParent.lossyScale;
        transform.localScale = new Vector3(
            originalWorldScale.x / parentScale.x,
            originalWorldScale.y / parentScale.y,
            originalWorldScale.z / parentScale.z
        );
    }
}
