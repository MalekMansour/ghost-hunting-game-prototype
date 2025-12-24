using UnityEngine;

public class Candle : MonoBehaviour
{
    public GameObject flameObject;
    public Light candleLight;

    void Start()
    {
        flameObject.SetActive(false);
        candleLight.enabled = false;
    }

    public void LightCandle()
    {
        flameObject.SetActive(true);
        candleLight.enabled = true;
    }
}
