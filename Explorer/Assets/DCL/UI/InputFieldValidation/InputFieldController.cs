using JetBrains.Annotations;
using TMPro;
using UnityEngine.EventSystems;

namespace DCL.UI.InputFieldValidator
{
    /// <summary>
    /// This controller serves as an in-between other classes and the TMP_InputField, capturing events and
    /// making sure that if a formatter is referenced a properly formatted text is submitted.
    /// Also checks the size of inserted and replaced text, to make sure the input field limits aren't exceeded.
    /// </summary>
    public class InputFieldController
    {
       public delegate void InputFieldInputChangedDelegate(string changedText);
       public delegate void InputFieldSelectionChangedDelegate(bool isSelected);
       public delegate void InputFieldSubmitDelegate(string submittedInput);

       private readonly TMP_InputField inputField;
       [CanBeNull] private readonly ITextFormatter chatInputFormatter;

       private int lastTextLenght;

       public InputFieldController(TMP_InputField inputField, ITextFormatter chatInputFormatter = null)
       {
           this.inputField = inputField;
           this.chatInputFormatter = chatInputFormatter;

           inputField.onValueChanged.AddListener(OnInputChanged);
           inputField.onSubmit.AddListener(OnSubmit);
           inputField.onSelect.AddListener(OnInputFieldSelected);
           inputField.onDeselect.AddListener(OnInputFieldDeselected);
       }

       public int CharacterLimit => inputField.characterLimit;
        public int TextLength => inputField.text.Length;
        public string InputText => inputField.text;
        public bool IsFocused => inputField.isFocused;

        public event InputFieldInputChangedDelegate InputChangedEvent;
        public event InputFieldSubmitDelegate InputFieldSubmitEvent;
        public event InputFieldSelectionChangedDelegate InputFieldSelectionChangedEvent;

        public void DeactivateInputField() =>
            inputField.DeactivateInputField();

        public void ActivateInputField() =>
            inputField.ActivateInputField();

        /// <summary>
        ///     Sets the current content of the input field.
        /// </summary>
        /// <param name="text">The new content of the input field </param>
        /// <param name="notify">If the text change should be notified, sending OnValueChanged event</param>
        public void SetText(string text, bool notify = true)
        {
            if (notify)
                inputField.text = text;
            else
                inputField.SetTextWithoutNotify(text);
        }

        public bool IsWithinCharacterLimit(int newTextLenght = 0) =>
            inputField.text.Length + newTextLenght < inputField.characterLimit;

        public void InsertTextAtSelectedPosition(string text)
        {
            InsertTextAtPosition(text, inputField.stringPosition);
            inputField.OnSelect(null);
        }

        /// <summary>
        ///     Selects the input field, gives it focus, replaces its content if the text is not null and moves the caret to its correct position.
        /// </summary>
        /// <param name="text">The new content of the input field, can be null </param>
        public void SelectInputField(string text = null)
        {
            inputField.OnSelect(null);

            if (text == null) return;

            inputField.text = text;
            inputField.caretPosition = inputField.text.Length;
        }

        public void ResetInputField()
        {
            inputField.text = string.Empty;
            ActivateInputField();
        }

        public void DeselectInputField()
        {
            inputField.OnDeselect(null);
        }

        /// <summary>
        ///     Manually sends an OnSubmit event from the InputField -- used by chat commands - probably needs to be removed
        /// </summary>
        public void SubmitInput(BaseEventData eventData)
        {
            inputField.OnSubmit(eventData);
        }

        /// <summary>
        ///     Only the text that fits inside the input field character limits will be inserted, anything else will be lost.
        /// </summary>
        private void InsertTextAtPosition(string pastedText, int position)
        {
            int remainingSpace = inputField.characterLimit - inputField.text.Length;

            if (remainingSpace <= 0) return;

            string textToInsert = pastedText.Length > remainingSpace ? pastedText[..remainingSpace] : pastedText;

            inputField.text = inputField.text.Insert(position, textToInsert);
            inputField.stringPosition = position + textToInsert.Length;
        }

        public void ReplaceText(string oldValue, string newValue)
        {
            int textLenghtDifference = newValue.Length - oldValue.Length;

            if (!IsWithinCharacterLimit(textLenghtDifference)) return;

            inputField.text = inputField.text.Replace(oldValue, newValue);
            inputField.stringPosition += textLenghtDifference;
        }

        /// <summary>
        ///     Before submitting the text is Formatted, so links are added and invalid rich text tags are converted to inert ones.
        /// </summary>
        private void OnSubmit(string text)
        {
            text = chatInputFormatter?.FormatText(text);
            InputFieldSubmitEvent?.Invoke(text);
        }

        private void OnInputFieldSelected(string _)
        {
            InputFieldSelectionChangedEvent?.Invoke(true);
        }

        private void OnInputFieldDeselected(string _)
        {
            InputFieldSelectionChangedEvent?.Invoke(false);
        }

        private void OnInputChanged(string text)
        {
            InputChangedEvent?.Invoke(text);
        }
    }
}
