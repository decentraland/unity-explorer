using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatInputView
    {
        event Action<string> OnMessageSubmitted;
        event Action<string> OnInputChanged;
        event Action<bool> OnFocusChanged;

        void SetInputEnabled(bool activate, string? maskMessage = null);
        void SetText(string text);
        void ShowMask(string message);
        void HideMask();
        string GetText();
        void Focus();
        void Blur();
        void Show();
        void Hide();
    }
}