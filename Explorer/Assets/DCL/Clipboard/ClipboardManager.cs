namespace DCL.Clipboard
{
    public interface IClipboardManager
    {
        public delegate void ClipboardCopyEventHandler(object sender, string copiedText);
        public delegate void ClipboardPasteEventHandler(object sender, string pastedText);


        event ClipboardCopyEventHandler? OnCopy;
        event ClipboardPasteEventHandler? OnPaste;

        void Copy(object sender, string text);

        void Paste(object sender);

        public bool HasValue();
    }

    public class ClipboardManager : IClipboardManager
    {
        private readonly ISystemClipboard systemClipboard;

        public ClipboardManager(ISystemClipboard systemClipboard)
        {
            this.systemClipboard = systemClipboard;
        }

        public event IClipboardManager.ClipboardCopyEventHandler? OnCopy;
        public event IClipboardManager.ClipboardPasteEventHandler? OnPaste;

        public bool HasValue() =>
            systemClipboard.HasValue();

        public void Copy(object sender, string text)
        {
            systemClipboard.Set(text);
            OnCopy?.Invoke(sender, text);
        }

        public void Paste(object sender)
        {
            string text = systemClipboard.Get();
            OnPaste?.Invoke(sender, text);
        }
    }
}
