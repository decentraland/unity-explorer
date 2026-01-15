using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ValidatedInputField
{
    /// <summary>
    ///     Reusable input field component with visual validation feedback.
    ///     Handles the visual state (outline color, error mark) based on validation result.
    ///     The actual validation logic should be provided externally via <see cref="SetValidationFunction" />.
    /// </summary>
    public class ValidatedInputFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_InputField InputField { get; private set; }

        [field: SerializeField]
        public Image InputOutline { get; private set; }

        [field: SerializeField]
        public Color OutlineNormalColor { get; private set; } = Color.white;

        [field: SerializeField]
        public Color OutlineErrorColor { get; private set; } = Color.red;

        [field: SerializeField]
        public GameObject ErrorMark { get; private set; }

        /// <summary>
        ///     Invoked when the input text changes. Passes the new text value.
        /// </summary>
        public event Action<string> OnValueChanged;

        /// <summary>
        ///     Invoked when validation state changes. True = valid, False = invalid.
        /// </summary>
        public event Action<bool> OnValidationStateChanged;

        private Func<string, bool> validationFunction;
        private bool lastValidationState = true;

        public string Text
        {
            get => InputField.text;
            set => InputField.text = value;
        }

        public bool IsValid => validationFunction == null || validationFunction(InputField.text);

        private void Awake()
        {
            if (InputField != null)
                InputField.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDestroy()
        {
            if (InputField != null)
                InputField.onValueChanged.RemoveListener(HandleValueChanged);
        }

        /// <summary>
        ///     Sets the validation function. The function should return true if the input is valid.
        ///     When set, validation will be performed automatically on each input change.
        /// </summary>
        /// <param name="validation">Function that takes the input text and returns true if valid.</param>
        public void SetValidationFunction(Func<string, bool> validation)
        {
            validationFunction = validation;
            ValidateAndUpdateVisuals();
        }

        /// <summary>
        ///     Manually shows or hides the error state.
        /// </summary>
        /// <param name="showError">True to show error state, false for normal state.</param>
        public void ShowError(bool showError)
        {
            if (InputOutline != null)
                InputOutline.color = showError ? OutlineErrorColor : OutlineNormalColor;

            if (ErrorMark != null)
                ErrorMark.SetActive(showError);
        }

        /// <summary>
        ///     Clears the input field and resets to normal visual state.
        /// </summary>
        public void Clear()
        {
            if (InputField != null)
                InputField.text = string.Empty;

            ShowError(false);
            lastValidationState = true;
        }

        /// <summary>
        ///     Forces validation and visual update.
        /// </summary>
        public void ValidateAndUpdateVisuals()
        {
            if (validationFunction == null)
            {
                ShowError(false);
                return;
            }

            string text = InputField != null ? InputField.text : string.Empty;
            bool isEmpty = string.IsNullOrEmpty(text);
            bool isValid = isEmpty || validationFunction(text);

            // Show error only when there's text and it's invalid
            ShowError(!isEmpty && !isValid);

            // Notify if validation state changed
            bool currentValidState = isEmpty ? lastValidationState : isValid;

            if (currentValidState != lastValidationState)
            {
                lastValidationState = currentValidState;
                OnValidationStateChanged?.Invoke(isValid);
            }
        }

        private void HandleValueChanged(string newValue)
        {
            OnValueChanged?.Invoke(newValue);
            ValidateAndUpdateVisuals();
        }
    }
}
