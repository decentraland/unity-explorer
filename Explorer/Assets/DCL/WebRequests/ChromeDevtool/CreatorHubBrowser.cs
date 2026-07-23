using CDPBridges;
using DCL.Diagnostics;
using Plugins.DclNativeProcesses;
using RichTypes;
using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.WebRequests.ChromeDevtool
{
    public class CreatorHubBrowser : IBrowser
    {
        /// <summary>
        ///     EditorPrefs key holding a developer-local override for the Creator Hub executable path.
        ///     It is assigned through the Debug Settings drawer and read only in the Editor: it is never
        ///     sourced from app-args or deep links (SEC-005) and is compiled out of player builds, so a
        ///     shipped client can only ever launch the pinned <see cref="DEFAULT_CREATOR_HUB_BIN_PATH" />.
        /// </summary>
        public const string BIN_PATH_EDITOR_PREF_KEY = "CreatorHubBrowser.BinPathOverride";

        private const string DEVTOOL_PORT_ARG = "--open-devtools-with-port=";

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        // path for: C:\Users\<YourUsername>\AppData\Local\Programs\creator-hub\Decentraland Creator Hub.exe
        public static readonly string DEFAULT_CREATOR_HUB_BIN_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "creator-hub", "Decentraland Creator Hub.exe"
            );
#else
        public static readonly string DEFAULT_CREATOR_HUB_BIN_PATH = "/Applications/Decentraland Creator Hub.app/Contents/MacOS/Decentraland Creator Hub";
#endif

        private readonly int port;

        public CreatorHubBrowser(int port)
        {
            this.port = port;
        }

        public BrowserOpenResult OpenUrl(string url)
        {
            string path = ResolveBinPath();

            if (File.Exists(path) == false)
            {
                BrowserOpenError error = BrowserOpenError.FromException(new Exception($"Creator Hub is not installed in path: {path}"));
                return BrowserOpenResult.FromBrowserOpenError(error);
            }

            ReportHub.LogWarning(ReportCategory.CHROME_DEVTOOL_PROTOCOL, "Url always ignored by Creator Hub Browser, port is used");

            Result result = DclProcesses.Start(path, new[] { $"{DEVTOOL_PORT_ARG}{port}" });

            if (result.Success == false)
            {
                BrowserOpenError error = BrowserOpenError.FromException(new Exception(result.ErrorMessage!));
                return BrowserOpenResult.FromBrowserOpenError(error);
            }

            return BrowserOpenResult.Success();
        }

        private static string ResolveBinPath()
        {
#if UNITY_EDITOR
            string overridePath = EditorPrefs.GetString(BIN_PATH_EDITOR_PREF_KEY, string.Empty);

            if (!string.IsNullOrEmpty(overridePath))
                return overridePath;
#endif
            return DEFAULT_CREATOR_HUB_BIN_PATH;
        }
    }
}
