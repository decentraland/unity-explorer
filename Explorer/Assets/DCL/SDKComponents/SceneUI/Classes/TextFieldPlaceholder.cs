using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class TextFieldPlaceholder : IDisposable
    {
        private TextField textField;

        private bool isPlaceholder;
        private bool isFocused;

        public bool IsReadonly { get; set; }
        public string PlaceholderText { get; set; }
        public Color PlaceholderColor { get; set; }
        public Color NormalColor { get; set; }

        public void Initialize(TextField textFieldToSetup, string placeholderText, Color placeholderColor)
        {
            textField = textFieldToSetup;
            textField.RegisterCallback<FocusInEvent>(OnFocusIn);
            textField.RegisterCallback<FocusOutEvent>(OnFocusOut);

            PlaceholderText = placeholderText;
            PlaceholderColor = placeholderColor;

            Refresh();
        }

        public void Refresh()
        {
            isPlaceholder = !isFocused && InputIsNullOrEmpty();

            if (isPlaceholder)
                SetPlaceholderStyle();
            else
                SetNormalStyle();
        }

        private void SetNormalStyle()
        {
            if (textField == null)
                return;

            textField.style.color = NormalColor;
        }

        private void SetPlaceholderStyle()
        {
            if (textField == null)
                return;

            textField.style.color = PlaceholderColor;
            textField.SetValueWithoutNotify(PlaceholderText);
        }

        private void OnFocusIn(FocusInEvent _)
        {
            if (textField == null || IsReadonly)
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
            if (textField == null)
                return;

            if (IsReadonly)
                return;

            isFocused = false;
            Refresh();
        }

        private bool InputIsNullOrEmpty() =>
            string.IsNullOrEmpty(textField.text);

        public void Dispose()
        {
            if (textField == null)
                return;

            textField.UnregisterCallback<FocusInEvent>(OnFocusIn);
            textField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }
    }
}
