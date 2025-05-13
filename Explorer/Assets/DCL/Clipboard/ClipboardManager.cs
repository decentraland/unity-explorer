using System.Text.RegularExpressions;

namespace DCL.Clipboard
{
    /// <summary>
    ///     Implementation of the IClipboardManager, it is a wrapper for the clipboard, providing events and methods to more easily react to changes in it.
    /// </summary>
    public class ClipboardManager : IClipboardManager
    {
        private readonly ISystemClipboard systemClipboard;
        private static readonly Regex TAG_REGEX = new(@"<[^>]*>", RegexOptions.Compiled);

        public ClipboardManager(ISystemClipboard systemClipboard)
        {
            this.systemClipboard = systemClipboard;
        }

        public event IClipboardManager.CopyEventHandler? OnCopy;
        public event IClipboardManager.PasteEventHandler? OnPaste;

        public bool HasValue() =>
            systemClipboard.HasValue();

        /// <summary>
        /// Sets the text on the clipboard and then calls an OnCopy event.
        /// </summary>
        /// <param name="sender"> The sender object </param>
        /// <param name="text"> The text to copy</param>
        public void Copy(object sender, string text)
        {
            systemClipboard.Set(text);
            OnCopy?.Invoke(sender, text);
        }

        /// <summary>
        /// Sanitizes the text by removing all rich text tags from it and then does a normal Copy
        /// </summary>
        /// <param name="sender"> The sender object </param>
        /// <param name="text"> The text to sanitize and copy </param>
        public void CopyAndSanitize(object sender, string text)
        {
            string sanitizedString = TAG_REGEX.Replace(text, "");
            Copy(sender, sanitizedString);
        }

        /// <summary>
        ///  Gets the text on the clipboard and invokes an OnPaste event
        /// </summary>
        /// <param name="sender"> The sender object </param>
        public void Paste(object sender)
        {
            string text = systemClipboard.Get();
            OnPaste?.Invoke(sender, text);
        }
    }
}
