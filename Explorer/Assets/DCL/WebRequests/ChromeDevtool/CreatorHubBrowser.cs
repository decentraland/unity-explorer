using CDPBridges;
using Crosstales.FB;
using DCL.Diagnostics;
using DCL.Prefs;
using Plugins.DclNativeProcesses;
using RichTypes;
using System;
using System.IO;

namespace DCL.WebRequests.ChromeDevtool
{
    public class CreatorHubBrowser : IBrowser
    {
        private const string DEVTOOL_PORT_ARG = "--open-devtools-with-port=";

        private readonly int port;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        // path for: C:\Users\<YourUsername>\AppData\Local\Programs\creator-hub\Decentraland Creator Hub.exe
        public static readonly string DEFAULT_CREATOR_HUB_BIN_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "creator-hub", "Decentraland Creator Hub.exe"
            );

        private const string EXECUTABLE_EXTENSION = "exe";
#else
        public static readonly string DEFAULT_CREATOR_HUB_BIN_PATH = "/Applications/Decentraland Creator Hub.app/Contents/MacOS/Decentraland Creator Hub";

        private const string EXECUTABLE_EXTENSION = "*";
#endif

        public CreatorHubBrowser(int port)
        {
            this.port = port;
        }

        public BrowserOpenResult OpenUrl(string url)
        {
            // Resolve lazily: the CDP bridge is the only consumer of the Creator Hub path, so we only
            // touch PlayerPrefs / prompt the developer when DevTools actually needs to launch it.
            string path = ResolveBinPath();

            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
            {
                BrowserOpenError error = BrowserOpenError.FromException(new Exception($"Creator Hub executable not found or not selected (resolved path: '{path}')"));
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

        /// <summary>
        ///     Resolves the Creator Hub executable: a path the developer previously selected (remembered in
        ///     <see cref="DCLPlayerPrefs" />) wins; otherwise the pinned default install path; otherwise the
        ///     developer is prompted once to locate it and the choice is remembered. There is no app-arg /
        ///     deep-link input (SEC-005) — the path is either the known install location or a local file the
        ///     developer explicitly picks.
        /// </summary>
        private static string ResolveBinPath()
        {
            string savedPath = DCLPlayerPrefs.GetString(DCLPrefKeys.CREATOR_HUB_BIN_PATH);

            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                return savedPath;

            if (File.Exists(DEFAULT_CREATOR_HUB_BIN_PATH))
                return DEFAULT_CREATOR_HUB_BIN_PATH;

            string picked = PromptForExecutable();

            if (!string.IsNullOrEmpty(picked))
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CREATOR_HUB_BIN_PATH, picked, save: true);
                return picked;
            }

            return string.Empty;
        }

        private static string PromptForExecutable()
        {
            FileBrowser fileBrowser = FileBrowser.Instance;

            // Enable synchronous picking on macOS (off by default) so the dialog fits OpenUrl's synchronous contract.
            fileBrowser.AllowSyncCalls = true;

            string startDirectory = File.Exists(DEFAULT_CREATOR_HUB_BIN_PATH)
                ? Path.GetDirectoryName(DEFAULT_CREATOR_HUB_BIN_PATH) ?? string.Empty
                : string.Empty;

            string? selected = fileBrowser.OpenSingleFile("Select the Decentraland Creator Hub executable", startDirectory, string.Empty, EXECUTABLE_EXTENSION);

            return selected ?? string.Empty;
        }
    }
}
