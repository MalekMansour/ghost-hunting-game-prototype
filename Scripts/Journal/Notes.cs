using TMPro;
using UnityEngine;

public class Notes : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_InputField notesInput;
    [SerializeField] private RectTransform textViewport; // the visible notes area

    private string lastValidText = "";

    private void Awake()
    {
        if (!notesInput)
            notesInput = GetComponentInChildren<TMP_InputField>(true);

        if (!notesInput)
        {
            Debug.LogError("[JournalNotes] No TMP_InputField found.");
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

    private void OnTextChanged(string newText)
    {
        // Force TMP to update layout so sizes are accurate
        Canvas.ForceUpdateCanvases();

        TMP_Text textComponent = notesInput.textComponent;

        // Height of rendered text
        float textHeight = textComponent.preferredHeight;

        // Height of the visible notes area
        float viewportHeight = textViewport.rect.height;

        if (textHeight <= viewportHeight + 0.5f)
        {
            // Text still fits → accept
            lastValidText = newText;
        }
        else
        {
            // Text overflowed → revert
            notesInput.SetTextWithoutNotify(lastValidText);

            // Keep caret at end so it feels natural
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
    }

    public string GetNotes() => lastValidText;

    public void SetNotes(string text)
    {
        lastValidText = text ?? "";
        notesInput.SetTextWithoutNotify(lastValidText);
    }
}
