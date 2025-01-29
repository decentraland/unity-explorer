using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.InputFieldValidator
{
    [RequireComponent(typeof(TMP_InputField))]
    public class ValidatedInputFieldElement : MonoBehaviour
    {
        public delegate void InputFieldInputValidatedDelegate(string validatedInput);
        public delegate void InputFieldSelectionChangedDelegate(bool isSelected);
        public delegate void InputFieldSubmitDelegate(string submittedInput);

        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private InputFieldsValidator fieldsValidator;

        private int lastTextLenght;

        public int CharacterLimit => inputField.characterLimit;
        public int TextLength => inputField.text.Length;
        public string InputText => inputField.text;
        public bool IsFocused => inputField.isFocused;

        private void Awake()
        {
            inputField.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            inputField.inputValidator = fieldsValidator;
            fieldsValidator.InitializeStyles();
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

            //When inserting text we need to add each character individually to properly parse the text as it relies on a per-character validation
            foreach (char c in textToInsert)
            {
                inputField.text = inputField.text.Insert(position, c.ToString());
                position++;
            }

            inputField.stringPosition += textToInsert.Length;
        }

        private void Submit(string text)
        {
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
                fieldsValidator.ValidateOnBackspace(ref text, ref position);
                inputField.SetTextWithoutNotify(text);
                inputField.stringPosition = position;
            }

            lastTextLenght = text.Length;
            InputValidated?.Invoke(text);
        }
    }
}
