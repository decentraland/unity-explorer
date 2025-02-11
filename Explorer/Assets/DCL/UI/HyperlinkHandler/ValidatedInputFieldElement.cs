using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace DCL.UI.InputFieldValidator
{
    /// <summary>
    /// This class serves as an in-between other classes and the TMP_InputField, allowing to validate the text input and to access the validated text
    /// This is needed because the normal TMP_InputField doesn't validate the input text always, but only when is entered directly through the input field
    /// When assigning text to the input text (like inputField.text = "something") it won't perform a validation
    /// When removing text through the backspace or delete keys, it won't validate text either
    /// So this requires an intermediary class that forces this logic so all text input into the field is validated and formatted properly.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class ValidatedInputFieldElement : MonoBehaviour
    {
        public delegate void InputFieldInputValidatedDelegate(string validatedInput);
        public delegate void InputFieldSelectionChangedDelegate(bool isSelected);
        public delegate void InputFieldSubmitDelegate(string submittedInput);

        [SerializeField] private TMP_InputField inputField;
        [FormerlySerializedAs("fieldsValidator")] [SerializeField] private InputFieldValidator inputFieldValidator;

        private int lastTextLenght;

        public int CharacterLimit => inputField.characterLimit;
        public int TextLength => inputField.text.Length;
        public string InputText => inputField.text;
        public bool IsFocused => inputField.isFocused;

        private void Awake()
        {
            inputField.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            inputField.inputValidator = inputFieldValidator;
            inputFieldValidator.InitializeStyles();
            inputField.onValueChanged.AddListener(Validate);
            inputField.onSubmit.AddListener(Submit);
            inputField.onSelect.AddListener(OnInputFieldSelected);
            inputField.onDeselect.AddListener(OnInputFieldDeselected);
        }

        public event InputFieldSubmitDelegate InputValidated;
        public event InputFieldInputValidatedDelegate InputFieldSubmit;
        public event InputFieldSelectionChangedDelegate InputFieldSelectionChanged;

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

        private void InsertTextAtPosition(string pastedText, int position)
        {
            int remainingSpace = inputField.characterLimit - inputField.text.Length;

            if (remainingSpace <= 0) return;

            string textToInsert = pastedText.Length > remainingSpace ? pastedText[..remainingSpace] : pastedText;

            var newText = inputField.text.Insert(position, textToInsert);
            var newPosition = position + newText.Length;

            inputFieldValidator.Validate(ref newText, ref newPosition);
            inputField.text = newText;

            inputField.stringPosition = newPosition;
        }

        //We do a validation before submitting
        private void Submit(string text)
        {
            var position = 0;
            inputFieldValidator.Validate(ref text, ref position);
            InputFieldSubmit?.Invoke(text);
        }

        private void OnInputFieldSelected(string _)
        {
            InputFieldSelectionChanged?.Invoke(true);
        }

        private void OnInputFieldDeselected(string _)
        {
            InputFieldSelectionChanged?.Invoke(false);
        }

        private void Validate(string text)
        {
            if (lastTextLenght > text.Length)
            {
                int position = inputField.stringPosition;
                inputFieldValidator.Validate(ref text, ref position);
                inputField.SetTextWithoutNotify(text);
                inputField.stringPosition = position;
            }

            // We dont show rich text when the chat is in command "mode", to avoid having hidden text.
            // As soon as the bar is removed, the input is re-validated and all invalid formats are removed
            inputField.richText = !text.StartsWith("/");

            lastTextLenght = text.Length;
            InputValidated?.Invoke(text);
        }
    }
}
