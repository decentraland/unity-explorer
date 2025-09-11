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
        private static readonly string DEFAULT_CREATOR_HUB_BIN_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "creator-hub", "Decentraland Creator Hub.exe"
            );
#else
        private static readonly string DEFAULT_CREATOR_HUB_BIN_PATH = "/Applications/Decentraland Creator Hub.app/Contents/MacOS/Decentraland Creator Hub";
#endif

        public CreatorHubBrowser(IAppArgs appArgs, int port)
        {
            this.appArgs = appArgs;
            this.port = port;
        }

        public BrowserOpenResult OpenUrl(string url)
        {
            if (appArgs.TryGetValue(AppArgsFlags.CREATOR_HUB_BIN_PATH, out string? path) == false)
            {
                ReportHub.LogWarning(ReportCategory.CHROME_DEVTOOL_PROTOCOL, "Creator Hub path is not provided, fallback to default path");
                path = DEFAULT_CREATOR_HUB_BIN_PATH;
            }

            ReportHub.LogWarning(ReportCategory.CHROME_DEVTOOL_PROTOCOL, "Url always ignored by Creator Hub Browser, port is used");

            Result result = DclProcesses.Start(
                path!,
                new[]
                {
                    $"{DEVTOOL_PORT_ARG}{port}",
                }
            );

            if (result.Success == false)
            {
                BrowserOpenError error = BrowserOpenError.FromException(new Exception(result.ErrorMessage!));
                return BrowserOpenResult.FromBrowserOpenError(error);
            }

            return BrowserOpenResult.Success();
        }
    }
}
