using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatInputView : MonoBehaviour, IChatInputView
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private GameObject inputFieldContainer;
        [SerializeField] private Button maskButton;
        [SerializeField] private GameObject maskObject;
        [SerializeField] private TMP_Text maskText;
        
        [Header("Focus Visuals")]
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private GameObject characterCounterObject;
        [SerializeField] private GameObject emojiButtonObject;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color focusedBackgroundColor;
        [SerializeField] private Color unfocusedBackgroundColor;
        
        public event Action<string>? OnMessageSubmitted;
        public event Action<string>? OnInputChanged;
        public event Action? OnFocusRequested;

        void Awake()
        {
            inputField.onValueChanged.AddListener((text) => OnInputChanged?.Invoke(text));
            inputField.onSubmit.AddListener((text) => OnMessageSubmitted?.Invoke(text));
            inputField.onSelect.AddListener((_) =>
            {
                if (inputFieldContainer.activeSelf)
                {
                    OnFocusRequested?.Invoke();
                }
            });
            
            maskButton.onClick.AddListener(() => OnFocusRequested?.Invoke());
        }
        
        private void SetFocusVisuals(bool isFocused)
        {
            outlineObject.SetActive(isFocused);
            characterCounterObject.SetActive(isFocused);
            emojiButtonObject.SetActive(isFocused);

            if (backgroundImage != null) backgroundImage.color = isFocused ? focusedBackgroundColor : unfocusedBackgroundColor;
        }
        
        public void Blur()
        {
            SetMode(IChatInputView.Mode.InactiveAsButton);
            inputField.DeactivateInputField();
        }
        
        public void SetMode(IChatInputView.Mode mode, string buttonMessage = "Type a message...")
        {
            switch (mode)
            {
                case IChatInputView.Mode.Active:
                    inputFieldContainer.SetActive(true);
                    SetFocusVisuals(true);
                    maskObject.SetActive(false);
                    break;
                case IChatInputView.Mode.InactiveAsButton:
                    inputFieldContainer.SetActive(false);
                    maskObject.SetActive(true);
                    maskText.text = buttonMessage;
                    break;
            }
        }

        public void SetText(string text) => inputField.text = text;
        public string GetText() => inputField.text;

        public void Focus()
        {
            SetMode(IChatInputView.Mode.Active);
            inputField.Select();
            inputField.ActivateInputField();
        }
        
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
        
        public void SetInputEnabled(bool activate, string? maskMessage = null)
        {
        }

        public void ShowMask(string message)
        {
        }

        public void HideMask()
        {
        }
    }
}