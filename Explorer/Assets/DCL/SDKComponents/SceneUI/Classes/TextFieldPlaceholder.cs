using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class TextFieldPlaceholder : IDisposable
    {
        private readonly TextField textField;

        private string placeholder;
        private Color placeholderColor;
        private Color normalColor;
        private bool isPlaceholder;
        private bool isFocused;
        private bool isReadonly;

        public TextFieldPlaceholder(TextField textField)
        {
            this.textField = textField;

            textField.RegisterCallback<FocusInEvent>(OnFocusIn);
            textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            // To support changing the value from code
            textField.RegisterValueChangedCallback(OnValueChanged);

            OnFocusOut(null);
        }

        public void SetPlaceholder(string placeholderValue)
        {
            this.placeholder = placeholderValue;

            if (isPlaceholder)
                UpdateIfFocusStateIs(false);
        }

        public void SetNormalColor(Color color)
        {
            normalColor = color;

            if (!isPlaceholder || isFocused)
                SetNormalStyle();
        }

        public void SetReadOnly(bool isReadOnly) =>
            this.isReadonly = isReadOnly;

        public void SetPlaceholderColor(Color color)
        {
            placeholderColor = color;

            if (isPlaceholder)
                UpdateIfFocusStateIs(false);
        }

        public void Refresh() =>
            OnFocusOut(null);

        private void UpdateIfFocusStateIs(bool focusState)
        {
            if (isFocused != focusState)
                return;

            if (focusState)
                SetNormalStyle();
            else
                SetPlaceholderStyle();
        }

        private void SetNormalStyle() =>
            textField.style.color = normalColor;

        private void SetPlaceholderStyle()
        {
            textField.style.color = placeholderColor;
            textField.SetValueWithoutNotify(placeholder);
        }

        private void OnFocusIn(FocusInEvent _)
        {
            if (isReadonly)
                return;

            if (isPlaceholder)
            {
                textField.SetValueWithoutNotify(string.Empty);
                SetNormalStyle();
            }

            isFocused = true;
        }

        private void OnFocusOut(FocusOutEvent _)
        {
            if (isReadonly)
                return;

            if (InputIsNullOrEmpty())
            {
                SetPlaceholderStyle();
                isPlaceholder = true;
            }
            else
            {
                SetNormalStyle();
                isPlaceholder = false;
            }

            isFocused = false;
        }

        private bool InputIsNullOrEmpty() =>
            string.IsNullOrEmpty(textField.text);

        private void OnValueChanged(ChangeEvent<string> newValue)
        {
            if (!isFocused)
                OnFocusOut(null);
        }

        public void Dispose()
        {
            textField.UnregisterCallback<FocusInEvent>(OnFocusIn);
            textField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            textField.UnregisterValueChangedCallback(OnValueChanged);
        }
    }
}
