using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatInputView
    {
        enum Mode { Active, InactiveAsButton }
        event Action<string> OnMessageSubmitted;
        event Action<string> OnInputChanged;
        event Action OnFocusRequested;

        void SetMode(Mode mode, string buttonMessage = "");
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