using CDPBridges;
using DCL.Diagnostics;
using Global.AppArgs;
using Plugins.DclNativeProcesses;
using RichTypes;
using System;

namespace DCL.WebRequests.ChromeDevtool
{
    public class CreatorHubBrowser : IBrowser
    {
        private const string DEVTOOL_PORT_ARG = "--open-devtools-with-port=";
        private readonly IAppArgs appArgs;
        private readonly int port;

        public CreatorHubBrowser(IAppArgs appArgs, int port)
        {
            this.appArgs = appArgs;
            this.port = port;
        }

        public BrowserOpenResult OpenUrl(string url)
        {
            if (appArgs.TryGetValue(AppArgsFlags.CREATOR_HUB_BIN_PATH, out string? path) == false)
            {
                Exception exception = new Exception("Creator Hub path is not provided");
                BrowserOpenError error = BrowserOpenError.FromException(exception);
                return BrowserOpenResult.FromBrowserOpenError(error);
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
