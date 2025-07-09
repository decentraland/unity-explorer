using System;
using TMPro;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatInputView : MonoBehaviour, IChatInputView
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private GameObject maskObject;
        [SerializeField] private TMP_Text maskText;

        public event Action<string>? OnMessageSubmitted;
        public event Action<string>? OnInputChanged;
        public event Action<bool>? OnFocusChanged;

        public void SetInputEnabled(bool activate, string? maskMessage = null)
        {
        }

        public void SetText(string text)
        {
        }

        public void ShowMask(string message)
        {
        }

        public void HideMask()
        {
        }

        public string GetText()
        {
            return inputField.text;
        }

        public void Focus()
        {
        }

        public void Blur()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}