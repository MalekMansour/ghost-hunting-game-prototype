using UnityEngine;

public class ModelScaler : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Assign the root of the mesh/armature for this character prefab.")]
    public Transform visualRoot;

    [Header("Size (in meters) - set these in Inspector per character")]
    [Tooltip("Desired total character height (feet to top of head). Example: 1.75")]
    public float targetHeightMeters = 1.75f;

    [Tooltip("Desired character width (shoulders-ish). Example: 0.55")]
    public float targetWidthMeters = 0.55f;

    [Tooltip("If true: scale based only on height. If false: uses the stricter of height+width.")]
    public bool fitByHeightOnly = true;

    [Header("Alignment")]
    [Tooltip("After scaling, place feet on this local Y plane (relative to THIS prefab). Usually 0.")]
    public float feetToLocalY = 0f;

    [Tooltip("Optional: set this to an Eye transform (head/eyes bone) to align to camera height.")]
    public Transform eyeAnchor;

    [Tooltip("If eyeAnchor is assigned, move the model so eyeAnchor sits at this local Y height.")]
    public float desiredEyeLocalY = 1.6f;

    [Tooltip("If true and eyeAnchor exists, eye alignment happens after feet alignment.")]
    public bool alignEyesToCameraHeight = true;

    [Header("Debug")]
    public bool logBounds = false;
    public bool applyOnStart = true;

    private void Start()
    {
        if (applyOnStart)
            ApplyScale();
    }

    [ContextMenu("Apply Scale Now")]
    public void ApplyScale()
    {
        if (visualRoot == null)
        {
            Debug.LogError($"[ModelScaler] ({name}) visualRoot is not assigned!");
            return;
        }

        // Collect renderers to compute bounds
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogError($"[ModelScaler] ({name}) No Renderers found under visualRoot. Cannot scale.");
            return;
        }

        // Compute world bounds first
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        float currentHeight = Mathf.Max(0.0001f, b.size.y);
        float currentWidth = Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));

        float scaleByHeight = targetHeightMeters / currentHeight;
        float scaleByWidth = targetWidthMeters / currentWidth;

        float chosenScale = fitByHeightOnly ? scaleByHeight : Mathf.Min(scaleByHeight, scaleByWidth);

        if (logBounds)
        {
            Debug.Log($"[ModelScaler] ({name}) BEFORE: height={currentHeight:F3} width={currentWidth:F3} " +
                      $"scaleByHeight={scaleByHeight:F3} scaleByWidth={scaleByWidth:F3} chosen={chosenScale:F3}");
        }

        // Apply uniform scaling to visualRoot (multiply existing scale)
        visualRoot.localScale = visualRoot.localScale * chosenScale;

        // Recompute bounds AFTER scaling
        renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        // 1) FEET ALIGNMENT: Move the model so its lowest point sits at feetToLocalY
        float worldFeetY = b.min.y;

        // Convert desired local plane to world Y
        float targetWorldFeetY = transform.TransformPoint(new Vector3(0f, feetToLocalY, 0f)).y;

        float deltaFeet = targetWorldFeetY - worldFeetY;

        // Move visualRoot in world space to correct feet
        visualRoot.position += new Vector3(0f, deltaFeet, 0f);

        // 2) EYE ALIGNMENT: optionally shift so eyeAnchor is at desiredEyeLocalY (local)
        if (alignEyesToCameraHeight && eyeAnchor != null)
        {
            float targetWorldEyeY = transform.TransformPoint(new Vector3(0f, desiredEyeLocalY, 0f)).y;
            float currentWorldEyeY = eyeAnchor.position.y;
            float deltaEye = targetWorldEyeY - currentWorldEyeY;

            visualRoot.position += new Vector3(0f, deltaEye, 0f);
        }

        if (logBounds)
        {
            float finalEyeY = eyeAnchor != null ? eyeAnchor.position.y : -999f;
            Debug.Log($"[ModelScaler] ({name}) AFTER: feetWorldY={(transform.TransformPoint(new Vector3(0f, feetToLocalY, 0f)).y):F3} " +
                      $"eyeWorldY={finalEyeY:F3} visualRootLocalScale={visualRoot.localScale}");
        }
    }
}
