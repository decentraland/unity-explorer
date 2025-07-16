using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatInputView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        public TMP_InputField InputField => inputField;
        
        [SerializeField] private GameObject inputFieldContainer;
        [SerializeField] private Button maskButton;
        [SerializeField] private GameObject maskContainer;
        [SerializeField] private TMP_Text maskText;
        
        [Header("Focus Visuals")]
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private GameObject characterCounterObject;
        [SerializeField] private GameObject emojiButtonObject;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color focusedBackgroundColor;
        [SerializeField] private Color unfocusedBackgroundColor;
        
        public event Action<string>? OnMessageSubmit;
        public event Action<string>? OnInputChanged;
        public event Action? OnFocusRequested;

        void Awake()
        {
            inputField.onValueChanged.AddListener((text) => OnInputChanged?.Invoke(text));
            inputField.onSubmit.AddListener((text) => OnMessageSubmit?.Invoke(text));
            inputField.onDeselect.AddListener(_ => ApplyUnfocusStyle());
            inputField.onSelect.AddListener((_) =>
            {
                if (inputFieldContainer.activeSelf)
                {
                    ApplyFocusStyle();
                    OnFocusRequested?.Invoke();
                }
            });
            
            maskButton.onClick.AddListener(() => OnFocusRequested?.Invoke());
        }
        
        private void ApplyFocusStyle()
        {
            outlineObject.SetActive(true);
            characterCounterObject.SetActive(true);
            emojiButtonObject.SetActive(true);
        }

        private void ApplyUnfocusStyle()
        {
            outlineObject.SetActive(false);
            characterCounterObject.SetActive(false);
            emojiButtonObject.SetActive(false);
        }
        
        
        public void SetText(string text) => inputField.text = text;
        public string GetText() => inputField.text;
        
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
        
        public void SetActiveTyping()
        {
            maskContainer.SetActive(false);
            inputFieldContainer.SetActive(true);
            inputField.Select();
            inputField.ActivateInputField();
        }

        public void SetDefault()
        {
            maskContainer.SetActive(false);
            inputFieldContainer.SetActive(true);
            ApplyUnfocusStyle();
        }

        public void SetBlocked(string reason)
        {
            inputFieldContainer.SetActive(false);
            maskContainer.SetActive(true);
            maskText.text = reason;
        }
    }
}