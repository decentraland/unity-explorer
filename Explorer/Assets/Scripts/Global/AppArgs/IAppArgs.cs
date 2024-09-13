using System.Collections.Generic;

namespace Global.AppArgs
{
    public interface IAppArgs
    {
        public const string DEBUG_FLAG = "debug";

        bool HasFlag(string flagName);

        bool TryGetValue(string flagName, out string? value);

        IEnumerable<string> Flags();
    }

    public static class AppArgsExtensions
    {
        public static bool HasDebugFlag(this IAppArgs args) =>
            true;
    }
}
