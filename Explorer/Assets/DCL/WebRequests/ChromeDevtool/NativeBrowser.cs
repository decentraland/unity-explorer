using CDPBridges;
using Plugins.DclNativeProcesses;
using RichTypes;
using System;

namespace DCL.WebRequests.ChromeDevtool
{
    public class NativeBrowser : IBrowser
    {
        public BrowserOpenResult OpenUrl(string url)
        {
            try
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                const string CHROME_PATH = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";

                if (!System.IO.File.Exists(CHROME_PATH))
                {
                    return BrowserOpenResult.FromBrowserOpenError(
                        BrowserOpenError.ErrorChromeNotInstalled()
                    );
                }

                Result result = DclProcesses.Start(
                    "open",
                    new[]
                    {
                        "-a",
                        "Google Chrome",
                        url,
                    }
                );

                if (result.Success == false)
                {
                    BrowserOpenError error = BrowserOpenError.FromException(new Exception(result.ErrorMessage!));
                    return BrowserOpenResult.FromBrowserOpenError(error);
                }
#else

                // Windows: check if Chrome is registered in the registry
                const string CHROME_KEY = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";
                object? path = Microsoft.Win32.Registry.GetValue(CHROME_KEY, "", null);

                if (path is not string chromeExePath || !System.IO.File.Exists(chromeExePath)) { return BrowserOpenResult.ErrorChromeNotInstalled(); }

                Result result = DclProcesses.Start(
                    "chrome",
                    new[] { url }
                );

                if (result.Success == false)
                {
                    BrowserOpenError error = BrowserOpenError.FromException(new Exception(result.ErrorMessage!));
                    return BrowserOpenResult.FromBrowserOpenError(error);
                }
#endif
                return BrowserOpenResult.Success();
            }
            catch (Exception e)
            {
                return BrowserOpenResult.FromBrowserOpenError(
                    BrowserOpenError.FromException(e)
                );
            }
        }
    }
}
