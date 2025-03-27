using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.ApplicationVersionGuard
{
    public class ApplicationVersionGuard
    {
        private const string LAUNCHER_EXECUTABLE_NAME = "Decentraland Launcher";
        private const string LAUNCHER_PATH_MAC = "/Applications/" + LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string LAUNCHER_PATH_WIN_MAIN = @"C:\Program Files\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_86 = @"C:\Program Files (x86)\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_COMBINED = @"Programs\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string DECENTRALAND_LAUNCHER_WIN_X64_EXE = "Decentraland-Launcher-win-x64.exe";
        private const string DECENTRALAND_LAUNCHER_MAC_ARM_64DMG = "Decentraland-Launcher-mac-arm64.dmg";
        private const string DECENTRALAND_LAUNCHER_MAC_X_64DMG = "Decentraland-Launcher-mac-x64.dmg";

        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;

        public ApplicationVersionGuard(IWebRequestController webRequestController, IWebBrowser webBrowser)
        {
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
        }

        public async UniTask<string> GetLatestVersionAsync(CancellationToken ct)
        {
            var response = await webRequestController.GetAsync(
                IDecentralandUrlsSource.EXPLORER_LATEST_RELEASE_URL,
                ReportCategory.VERSION_CONTROL,
                new WebRequestHeadersInfo())
                                                     .CreateFromJson<GitHubRelease>(WRJsonParser.Unity, ct);

            string latestVersion = response.tag_name.TrimStart('v');

            return latestVersion;
        }

        public async UniTask LaunchOrDownloadLauncherAsync(CancellationToken ct = default)
        {
            string? launcherPath = GetLauncherPath();

            if (string.IsNullOrEmpty(launcherPath))
            {
                await DownloadLauncherAsync(ct);
                Quit();
            }
            else
            {
                try
                {
                    await UniTask.Delay(1000, cancellationToken: ct);
                    ShellExecute(launcherPath);
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                        ReportHub.LogException(e, ReportCategory.VERSION_CONTROL);
                }
                finally
                {
                    await UniTask.Delay(2000, cancellationToken: ct);
                    Quit();
                }
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        private async UniTask DownloadLauncherAsync(CancellationToken ct)
        {
            string downloadUrl = await GetLauncherDownloadUrlAsync(ct);

            if (!string.IsNullOrEmpty(downloadUrl))
                webBrowser.OpenUrl(downloadUrl);
            else
                ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Failed to get launcher download URL.");

            return;

            async UniTask<string> GetLauncherDownloadUrlAsync(CancellationToken cancellationToken)
            {
                var response = await webRequestController.GetAsync(
                    IDecentralandUrlsSource.LAUNCHER_LATEST_RELEASE_URL,
                    ReportCategory.VERSION_CONTROL,
                    new WebRequestHeadersInfo())
                                                         .CreateFromJson<GitHubRelease>(WRJsonParser.Unity, cancellationToken);

                string version = response.tag_name.TrimStart('v');

                string assetName = GetLauncherAssetName();
                return $"{IDecentralandUrlsSource.LAUNCHER_DOWNLOAD_URL}/{version}/{assetName}";
            }
        }

        private static string GetLauncherAssetName()
        {
            return Application.platform switch
                   {
                       RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => DECENTRALAND_LAUNCHER_WIN_X64_EXE,
                       RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => SystemInfo.processorType.ToLower().Contains("arm") ? DECENTRALAND_LAUNCHER_MAC_ARM_64DMG : DECENTRALAND_LAUNCHER_MAC_X_64DMG,
                       _ => throw new NotSupportedException("Unsupported platform for launcher download."),
                   };
        }

        private static string? GetLauncherPath()
        {
            string[] possiblePaths;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    possiblePaths = new[]
                    {
                        LAUNCHER_PATH_WIN_MAIN,
                        LAUNCHER_PATH_WIN_86,
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LAUNCHER_PATH_WIN_COMBINED),
                    };

                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    possiblePaths = new[]
                    {
                        LAUNCHER_PATH_MAC,
                        $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}{LAUNCHER_PATH_MAC}",
                    };

                    break;
                default:
                    ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Unsupported platform for launching the application.");
                    return null;
            }

            return possiblePaths.FirstOrDefault(path => File.Exists(path)
                                                        || (Directory.Exists(path) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)));
        }

        private static void ShellExecute(string fileName)
        {
#if UNITY_STANDALONE_WIN
            if ((int)ShellExecute(IntPtr.Zero, null, fileName, null, null, SW_NORMAL) <= 16)
            {
                int error = Marshal.GetLastWin32Error();
                var sb = new StringBuilder(1024);

                uint length = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, error, 0, sb,
                    sb.Capacity, IntPtr.Zero);

                string message = length > 0 ? sb.ToString() : $"error {error}";
                throw new Win32Exception(error, message.TrimEnd());
            }
#elif UNITY_STANDALONE_OSX
            string cmd = "open \"" + fileName + "\"";
            int code = ExecuteSystemCommand(cmd);
            if (code != 0)
            {
                var sb = new StringBuilder(1024);

                string message = code == -1 && strerror_r(code, sb, sb.Capacity) == 0
                    ? sb.ToString()
                    : "Unknown error";
                throw new Exception($"error {code}: {message}");
            }
#endif
        }

#if UNITY_STANDALONE_WIN
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, int dwMessageId,
            uint dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr Arguments);

        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        [DllImport("shell32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr ShellExecute(IntPtr hWnd, string lpOperation, string lpFile,
            string lpParameters, string lpDirectory, int nShowCmd);

        private const int SW_NORMAL = 1;
#elif UNITY_STANDALONE_OSX
        [DllImport("libc", EntryPoint = "system")]
        private static extern int ExecuteSystemCommand([MarshalAs(UnmanagedType.LPStr)] string command);

        [DllImport("libc")]
        private static extern int strerror_r(int errnum, StringBuilder buf, int buflen);
#endif

        [Serializable]
        private struct GitHubRelease
        {
            public string tag_name;
        }
    }
}
