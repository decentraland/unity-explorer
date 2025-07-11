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
        
        public void Blur()
        {
            //SetMode(IChatInputView.Mode.InactiveAsButton);
            inputField.DeactivateInputField();
        }
        
        public void SetMode(IChatInputView.Mode mode, string buttonMessage = "Type a message...")
        {
            switch (mode)
            {
                case IChatInputView.Mode.Active:
                    inputFieldContainer.SetActive(true);
                    ApplyFocusStyle();
                    maskContainer.SetActive(false);
                    break;
                case IChatInputView.Mode.InactiveAsButton:
                    inputFieldContainer.SetActive(false);
                    maskContainer.SetActive(true);
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

        /// <summary>
        /// Sets the interactable state of the input view.
        /// </summary>
        /// <param name="isInteractable">If true, the input field is shown. If false, the mask is shown.</param>
        /// <param name="maskMessage">The message to display on the mask when not interactable. Can be null if interactable.</param>
        public void SetInteractable(bool isInteractable, string? maskMessage = null)
        {
            if (isInteractable)
            {
                Focus();
            }
            else
            {
                inputFieldContainer.SetActive(false);
                maskContainer.SetActive(true);
                maskText.text = maskMessage ?? "Input is disabled.";
            }
        }
    }
}