using System;
using System.IO;

namespace DCL.Utility
{
    /// <summary>
    ///     The Decentraland launcher's per-user application directory. The client exchanges file-based
    ///     bridges with the launcher inside it (deep-link, auth-token, crash-report session info), so the
    ///     location is defined once here instead of being copied into every consumer.
    /// </summary>
    public static class LauncherPaths
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        // C:\Users\<user>\AppData\Local\DecentralandLauncherLight\
        public static readonly string LauncherDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DecentralandLauncherLight"
            );
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        // ${XDG_DATA_HOME:-~/.local/share}/DecentralandLauncherLight/ — the launcher's app directory on
        // Linux (dirs::data_local_dir()); a sandboxed launcher hands the client the same XDG_DATA_HOME.
        public static readonly string LauncherDirectory =
            Path.Combine(
                Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdgDataHome
                    ? xdgDataHome
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share"),
                "DecentralandLauncherLight"
            );
#else
        // ~/Library/Application Support/DecentralandLauncherLight/
        public static readonly string LauncherDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library", "Application Support", "DecentralandLauncherLight"
            );
#endif

        public static string InLauncherDirectory(string fileName) =>
            Path.Combine(LauncherDirectory, fileName);
    }
}
