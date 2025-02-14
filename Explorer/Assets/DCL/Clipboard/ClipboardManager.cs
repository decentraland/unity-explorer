using System.Text.RegularExpressions;

namespace DCL.Clipboard
{
    public interface IClipboardManager
    {
        public delegate void CopyEventHandler(object sender, string copiedText);
        public delegate void PasteEventHandler(object sender, string pastedText);

        event CopyEventHandler? OnCopy;
        event PasteEventHandler? OnPaste;

        void Copy(object sender, string text);

        void CopyAndSanitize(object sender, string text);

        void Paste(object sender);

        bool HasValue();
    }

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

        public void Copy(object sender, string text)
        {
            systemClipboard.Set(text);
            OnCopy?.Invoke(sender, text);
        }

        public void CopyAndSanitize(object sender, string text)
        {
            string sanitizedString = TAG_REGEX.Replace(text, "");
            systemClipboard.Set(sanitizedString);
            OnCopy?.Invoke(sender, sanitizedString);
        }

        public void Paste(object sender)
        {
            string text = systemClipboard.Get();
            OnPaste?.Invoke(sender, text);
        }
    }
}
