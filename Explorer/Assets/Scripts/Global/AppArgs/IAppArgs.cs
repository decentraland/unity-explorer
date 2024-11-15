using System.Collections.Generic;

namespace Global.AppArgs
{
    public interface IAppArgs
    {
        public const string DEBUG_FLAG = "debug";
        public const string CAMERA_REELS_FLAG = "camerareels";

        bool HasFlag(string flagName);

        bool TryGetValue(string flagName, out string? value);

        IEnumerable<string> Flags();
    }

    public static class AppArgsExtensions
    {
        public static bool HasDebugFlag(this IAppArgs args) =>
            args.HasFlag(IAppArgs.DEBUG_FLAG);

        public static bool HasCameraReelsFlag(this IAppArgs args) =>
            args.HasFlag(IAppArgs.CAMERA_REELS_FLAG);
    }
}
