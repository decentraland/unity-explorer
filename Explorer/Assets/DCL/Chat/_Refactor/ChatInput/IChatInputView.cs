using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatInputView
    {
        enum Mode { Active, InactiveAsButton }
        event Action<string> OnMessageSubmit;
        event Action<string> OnInputChanged;
        event Action OnFocusRequested;

        void SetMode(Mode mode, string buttonMessage = "");
        void SetInteractable(bool isInteractable, string? maskMessage = null);
        void SetText(string text);
        string GetText();
        void Focus();
        void Blur();
        void Show();
        void Hide();
    }
}