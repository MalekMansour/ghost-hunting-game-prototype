using UnityEngine;

public class Units : MonoBehaviour
{
    [Header("Environmental Units (0 → Infinity)")]
    public float light = 0f;
    public float noise = 0f;
    public float heat = 0f;
    public float cold = 0f;
    public float smell = 0f;
    public float food = 0f;
    public float water = 0f;

    void OnDisable()
    {
        // Critical: no lingering signal when this component is OFF
        ZeroAll();
    }

    void OnDestroy()
    {
        ZeroAll();
    }

    public void ZeroAll()
    {
        light = 0f;
        noise = 0f;
        heat = 0f;
        cold = 0f;
        smell = 0f;
        food = 0f;
        water = 0f;
    }

    // Helpers (optional)
    public void SetNoise(float value)
    {
        noise = Mathf.Max(0f, value); // allow 0..infinity, block negatives
    }

    public void AddNoise(float value)
    {
        noise = Mathf.Max(0f, noise + value);
    }

    public void SilenceNoise()
    {
        noise = 0f;
    }
}
