using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.InputFieldValidator
{
    [RequireComponent(typeof(TMP_InputField))]
    public class ValidatedInputFieldElement : MonoBehaviour //Maybe this is better? analyze if it makes sense or not?!
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private InputFieldsValidator fieldsValidator;

        private int lastLenght;

        private void Awake()
        {
            inputField.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            inputField.inputValidator = fieldsValidator;
            fieldsValidator.InitializeStyles();
            inputField.onValueChanged.AddListener(Validate);
            inputField.onSubmit.AddListener(Submit);
        }

        public int GetTextLength() =>
            inputField.text.Length;

        public void DeactivateInputField() =>
            inputField.DeactivateInputField();

        public void ActivateInputField() =>
            inputField.ActivateInputField();

        public string GetText() =>
            inputField.text;

        public bool IsFocused() =>
            inputField.isFocused;

        /// <summary>
        ///     Represents the position of the caret in the real string (disregarding hidden or replaced text)
        ///     for example, an emoji occupies only 1 "space" visually but actually is represented internally in Unicode so it takes 10 chars in reality)
        ///     or for example a rich text tag is invisible, but still takes 3 or more spaces
        /// </summary>
        public int GetStringPosition() =>
            inputField.stringPosition;

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

        public event Action<string> OnInputValidated;
        public event Action<string> OnSubmit;

        /// <summary>
        ///     Selects the input field, gives it focus, replaces its content if the text is not null and moves the caret to its correct position.
        /// </summary>
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
        ///     Manually sends an OnSubmit event from the InputField
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

            //When inserting text we need to add each character individually to properly parse the pasted text as it relies on a per-character validation
            foreach (char c in textToInsert)
            {
                inputField.text = inputField.text.Insert(position, c.ToString());
                position++;
            }

            inputField.stringPosition += textToInsert.Length;
        }

        private void Submit(string text)
        {
            OnSubmit?.Invoke(text);
        }

        private void Validate(string text)
        {
            if (lastLenght > text.Length)
            {
                int position = inputField.stringPosition;
                fieldsValidator.ValidateOnBackspace(ref text, ref position);
                inputField.SetTextWithoutNotify(text);
                inputField.stringPosition = position;
            }

            lastLenght = text.Length;
            OnInputValidated?.Invoke(text);
        }
    }
}
