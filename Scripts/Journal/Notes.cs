using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Notes : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_InputField notesInput;
    [SerializeField] private RectTransform textViewport;

    private string lastValidText = "";

    public bool IsTyping => notesInput != null && notesInput.isFocused;
    

    private void Awake()
    {
        if (!notesInput)
            notesInput = GetComponentInChildren<TMP_InputField>(true);

        if (!notesInput)
        {
            Debug.LogError("[Notes] No TMP_InputField found.");
            enabled = false;
            return;
        }

        if (!textViewport)
            textViewport = notesInput.textViewport;

        notesInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        lastValidText = notesInput.text;

        notesInput.onValueChanged.AddListener(OnTextChanged);
    }

    private void OnDestroy()
    {
        if (notesInput)
            notesInput.onValueChanged.RemoveListener(OnTextChanged);
    }

    private void Update()
    {
        // If typing and player clicks anywhere outside the input field -> unfocus
        if (!IsTyping) return;

        if (Input.GetMouseButtonDown(0))
        {
            // If click is NOT on the notes input (or its children), unfocus
            if (!IsPointerOver(notesInput.gameObject))
                Unfocus();
        }
    }

    private bool IsPointerOver(GameObject go)
    {
        if (EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject == go || results[i].gameObject.transform.IsChildOf(go.transform))
                return true;
        }
        return false;
    }

    private void OnTextChanged(string newText)
    {
        Canvas.ForceUpdateCanvases();

        TMP_Text textComponent = notesInput.textComponent;
        float textHeight = textComponent.preferredHeight;
        float viewportHeight = textViewport.rect.height;

        if (textHeight <= viewportHeight + 0.5f)
        {
            lastValidText = newText;
        }
        else
        {
            notesInput.SetTextWithoutNotify(lastValidText);
            notesInput.caretPosition = lastValidText.Length;
        }
    }

    public void Focus()
    {
        notesInput.ActivateInputField();
        notesInput.Select();
    }

    public void Unfocus()
    {
        notesInput.DeactivateInputField();
        // This helps the EventSystem stop thinking we're still editing
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public string GetNotes() => lastValidText;

    public void SetNotes(string text)
    {
        lastValidText = text ?? "";
        notesInput.SetTextWithoutNotify(lastValidText);
    }
}
