using UnityEngine;
using UnityEngine.UI;

public class MarkOff : MonoBehaviour
{
    public enum MarkState { None, Circle, Cross }

    [Header("Mark Images")]
    [SerializeField] private GameObject circleGO;
    [SerializeField] private GameObject crossGO;

    [Header("Optional")]
    [SerializeField] private Button button;

    [SerializeField] private MarkState state = MarkState.None;

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();

        if (button)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        Apply();
    }

    private void OnDestroy()
    {
        if (button) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        // None -> Circle -> Cross -> None
        state = (MarkState)(((int)state + 1) % 3);
        Apply();
    }

    private void Apply()
    {
        if (circleGO) circleGO.SetActive(state == MarkState.Circle);
        if (crossGO)  crossGO.SetActive(state == MarkState.Cross);
    }

    // If you want to read/save later:
    public MarkState GetState() => state;

    public void SetState(MarkState newState)
    {
        state = newState;
        Apply();
    }

    public void ResetState()
    {
        state = MarkState.None;
        Apply();
    }
}
