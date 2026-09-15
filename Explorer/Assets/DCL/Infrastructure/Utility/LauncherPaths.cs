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
