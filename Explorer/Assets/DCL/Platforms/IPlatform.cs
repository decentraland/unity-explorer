namespace DCL.Platforms
{
    public interface IPlatform
    {
        enum Kind
        {
            Windows,
            Mac,
        }

        Kind CurrentPlatform();

        void Quit();

        static readonly IPlatform DEFAULT = new Platform();
    }

    public static class PlatformInfoExtensions
    {
        public static bool Is(this IPlatform platformInfo, IPlatform.Kind kind) =>
            platformInfo.CurrentPlatform() == kind;

        public static bool IsNot(this IPlatform platformInfo, IPlatform.Kind kind) =>
            platformInfo.Is(kind) == false;
    }
}
