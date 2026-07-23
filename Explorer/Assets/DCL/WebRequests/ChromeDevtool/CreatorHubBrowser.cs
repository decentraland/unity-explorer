using CDPBridges;
using DCL.Diagnostics;
using Global.AppArgs;
using Plugins.DclNativeProcesses;
using RichTypes;
using System;
using System.IO;

namespace DCL.WebRequests.ChromeDevtool
{
    public class CreatorHubBrowser : IBrowser
    {
        private const string DEVTOOL_PORT_ARG = "--open-devtools-with-port=";

        private readonly IAppArgs appArgs;
        private readonly int port;

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

        public CreatorHubBrowser(IAppArgs appArgs, int port)
        {
            this.appArgs = appArgs;
            this.port = port;
        }

        public BrowserOpenResult OpenUrl(string url)
        {
            string path = CreatorHubExecutablePath();

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

        /// <summary>
        ///     The Creator Hub executable to launch. Sourced from the <c>creator-hub-bin-path</c> app-arg
        ///     (command line / editor Debug Settings) — a trusted channel: the SEC-019 deny-by-default deep-link
        ///     allowlist drops this key, so an attacker-crafted <c>decentraland://</c> link can never set it.
        ///     When absent, falls back to the pinned <see cref="DEFAULT_CREATOR_HUB_BIN_PATH" />.
        /// </summary>
        private string CreatorHubExecutablePath()
        {
            if (appArgs.TryGetValue(AppArgsFlags.CREATOR_HUB_BIN_PATH, out string? path) && !string.IsNullOrEmpty(path))
                return path;

            return DEFAULT_CREATOR_HUB_BIN_PATH;
        }
    }
}
