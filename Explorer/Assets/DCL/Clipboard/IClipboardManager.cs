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
}
