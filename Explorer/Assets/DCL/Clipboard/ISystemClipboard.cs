namespace DCL.Clipboard
{
    public interface ISystemClipboard
    {
        void Set(string text);
        bool HasValue();
        string Get();
    }
}
