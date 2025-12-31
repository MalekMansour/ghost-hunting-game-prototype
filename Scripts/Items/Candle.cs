using UnityEngine;

public class Candle : MonoBehaviour
{
    [Header("Flame")]
    public GameObject flameObject;

    [Header("Light")]
    public Light candleLight;
    public float lightEnableDistance = 8f;

    [Header("Flame Units (on the flameObject)")]
    public Units flameUnits;                
    public float litLight = 3f;
    public float litHeat = 3f;

    private bool isLit = false;
    private Transform player;

    void Start()
    {
        if (flameObject) flameObject.SetActive(false);
        if (candleLight) candleLight.enabled = false;

        if (!flameUnits && flameObject)
            flameUnits = flameObject.GetComponent<Units>();

        if (Camera.main != null)
            player = Camera.main.transform;
    }

    void Update()
    {
        if (!isLit || candleLight == null || player == null)
            return;

        float dist = Vector3.Distance(player.position, transform.position);
        candleLight.enabled = dist <= lightEnableDistance;
    }

    public void LightCandle()
    {
        if (isLit) return;

        isLit = true;

        if (flameObject)
            flameObject.SetActive(true);

        if (flameUnits != null)
        {
            flameUnits.light = litLight;
            flameUnits.heat = litHeat;
        }
    }

    public void BlowOut()
    {
        if (!isLit) return;

        isLit = false;

        if (flameObject)
            flameObject.SetActive(false);

        if (candleLight)
            candleLight.enabled = false;

        if (flameUnits != null)
        {
            flameUnits.light = 0f;
            flameUnits.heat = 0f;
        }
    }

    public bool IsLit() => isLit;
}
