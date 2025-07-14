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
        void SetText(string text);
        string GetText();
        void Show();
        void Hide();
        
        /// <summary>
        /// Configures the view for active typing. Makes input field visible, focused, and styled for interaction.
        /// </summary>
        void SetActiveTyping();
    
        /// <summary>
        /// Configures the view for its default, idle state. Makes input field visible but unfocused.
        /// </summary>
        void SetDefault();

        /// <summary>
        /// Blocks the input field and displays a reason to the user.
        /// </summary>
        /// <param name="reason">The message to display on the mask (e.g., "User is offline").</param>
        void SetBlocked(string reason);
    }
}