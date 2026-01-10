using UnityEngine;
using System.Collections;

public class Sink : MonoBehaviour
{
    [Header("State")]
    public bool isOn = false;

    [Header("Water Visuals")]
    public GameObject waterParticles;
    public Transform waterSurface;

    [Header("Water Fill Animation")]
    [Tooltip("Local Y position when OFF. If left 0, captured from current localPosition.y on Start.")]
    public float waterOffLocalY = 0.0f;

    [Tooltip("How much the water rises when ON (meters). Example: 0.001 = 1mm.")]
    public float waterRiseAmount = 0.001f;

    [Tooltip("How fast the water rises/falls (meters per second).")]
    public float waterMoveSpeed = 0.0015f;

    [Header("Water Scale Animation")]
    [Tooltip("Local scale when OFF (small/empty). If left (0,0,0), captured from current localScale on Start.")]
    public Vector3 waterOffScale = Vector3.zero;

    [Tooltip("How much to grow the water surface when ON (usually X/Z).")]
    public Vector3 waterScaleIncrease = new Vector3(0.05f, 0f, 0.05f);

    [Tooltip("How fast the water scales (units per second).")]
    public float waterScaleSpeed = 0.15f;

    [Tooltip("Scale only X/Z (recommended for a flat water plane).")]
    public bool scaleOnlyXZ = true;

    [Header("Sound")]
    public AudioSource waterAudio;
    public AudioClip waterLoop;
    [Range(0f, 1f)] public float volume = 0.8f;

    float waterOnLocalY;
    Vector3 waterOnScale;
    Coroutine waterRoutine;

    void Start()
    {
        if (waterSurface != null)
        {
            // Capture OFF Y if not set
            if (Mathf.Approximately(waterOffLocalY, 0f))
                waterOffLocalY = waterSurface.localPosition.y;

            waterOnLocalY = waterOffLocalY + waterRiseAmount;

            // Capture OFF scale if not set
            if (waterOffScale == Vector3.zero)
                waterOffScale = waterSurface.localScale;

            // Compute ON scale
            waterOnScale = waterOffScale + waterScaleIncrease;

            // Start hidden + reset
            waterSurface.gameObject.SetActive(false);

            Vector3 p = waterSurface.localPosition;
            p.y = waterOffLocalY;
            waterSurface.localPosition = p;

            waterSurface.localScale = waterOffScale;
        }

        ApplyState(immediate: true);
    }

    public void Toggle()
    {
        isOn = !isOn;
        ApplyState(immediate: false);
    }

    void ApplyState(bool immediate)
    {
        if (waterParticles != null)
            waterParticles.SetActive(isOn);

        if (waterAudio != null)
        {
            if (isOn)
            {
                if (waterLoop != null)
                {
                    waterAudio.clip = waterLoop;
                    waterAudio.loop = true;
                    waterAudio.volume = volume;
                    if (!waterAudio.isPlaying) waterAudio.Play();
                }
            }
            else
            {
                waterAudio.Stop();
            }
        }

        if (waterSurface == null)
            return;

        float targetY = isOn ? waterOnLocalY : waterOffLocalY;
        Vector3 targetScale = isOn ? waterOnScale : waterOffScale;

        if (waterRoutine != null) StopCoroutine(waterRoutine);

        if (immediate)
        {
            if (isOn) waterSurface.gameObject.SetActive(true);

            SetWaterY(targetY);
            SetWaterScale(targetScale);

            if (!isOn) waterSurface.gameObject.SetActive(false);
        }
        else
        {
            // Turning ON: enable first so you can see it animating
            if (isOn) waterSurface.gameObject.SetActive(true);

            waterRoutine = StartCoroutine(AnimateWater(targetY, targetScale, turnOffAtEnd: !isOn));
        }
    }

    IEnumerator AnimateWater(float targetY, Vector3 targetScale, bool turnOffAtEnd)
    {
        // Keep animating until BOTH are basically at target
        while (true)
        {
            // Move Y
            float currentY = waterSurface.localPosition.y;
            float newY = Mathf.MoveTowards(currentY, targetY, Mathf.Max(0.0000001f, waterMoveSpeed) * Time.deltaTime);
            SetWaterY(newY);

            // Scale
            Vector3 currentS = waterSurface.localScale;
            Vector3 newS = Vector3.MoveTowards(currentS, targetScale, Mathf.Max(0.0000001f, waterScaleSpeed) * Time.deltaTime);
            SetWaterScale(newS);

            bool yDone = Mathf.Abs(waterSurface.localPosition.y - targetY) <= 0.00001f;
            bool sDone = (waterSurface.localScale - targetScale).sqrMagnitude <= 0.0000001f;

            if (yDone && sDone)
                break;

            yield return null;
        }

        // Snap exactly
        SetWaterY(targetY);
        SetWaterScale(targetScale);

        if (turnOffAtEnd)
            waterSurface.gameObject.SetActive(false);

        waterRoutine = null;
    }

    void SetWaterY(float y)
    {
        Vector3 p = waterSurface.localPosition;
        p.y = y;
        waterSurface.localPosition = p;
    }

    void SetWaterScale(Vector3 s)
    {
        if (scaleOnlyXZ)
        {
            Vector3 current = waterSurface.localScale;
            current.x = s.x;
            current.z = s.z;
            waterSurface.localScale = current;
        }
        else
        {
            waterSurface.localScale = s;
        }
    }
}
