using System;
using DCL.Clipboard;

namespace DCL.Chat.ChatCommands
{
    /// <summary>
    ///     Encapsulates the action of copying a given string to the system clipboard.
    /// </summary>
    public class CopyMessageCommand
    {
        private readonly ClipboardManager clipboardManager;

        public CopyMessageCommand(ClipboardManager clipboardManager)
        {
            this.clipboardManager = clipboardManager;
        }

        /// <summary>
        ///     Copies the provided text to the clipboard.
        /// </summary>
        /// <param name="textToCopy">The string to be copied.</param>
        public void Execute(object sender, string textToCopy)
        {
            // The presenter's 'this' context isn't needed for the core copy functionality.
            // If sanitization is required, it can be added here.
            clipboardManager.CopyAndSanitize(sender, textToCopy);
        }
    }
}