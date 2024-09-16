using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.ApplicationVersionGuard
{
    public class ApplicationVersionGuard
    {
        private const string EXPLORER_LATEST_RELEASE_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
        private const string LAUNCHER_LATEST_RELEASE_URL = "https://api.github.com/repos/decentraland/launcher/releases/latest";
        private const string LAUNCHER_DOWNLOAD_URL = "https://github.com/decentraland/launcher/releases/download";

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

        public async UniTask<(string current, string latest)> GetVersionsAsync(CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                EXPLORER_LATEST_RELEASE_URL,
                new FlatFetchResponse<GenericGetRequest>(),
                ct,
                ReportCategory.UNSPECIFIED,
                new WebRequestHeadersInfo());

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string latestVersion = latestRelease.tag_name.TrimStart('v');

            return (Application.version, latestVersion);
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
                ProcessStartInfo startInfo = PrepareLauncherStartInfo(launcherPath);

                try { Process.Start(startInfo); }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                        ReportHub.LogException(e, ReportCategory.UNSPECIFIED);
                }
                finally { Quit(); }
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
                ReportHub.LogError(ReportCategory.UNSPECIFIED, "Failed to get launcher download URL.");

            return;

            async UniTask<string> GetLauncherDownloadUrlAsync(CancellationToken cancellationToken)
            {
                FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                    LAUNCHER_LATEST_RELEASE_URL,
                    new FlatFetchResponse<GenericGetRequest>(),
                    cancellationToken,
                    ReportCategory.UNSPECIFIED,
                    new WebRequestHeadersInfo());

                GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
                string version = latestRelease.tag_name.TrimStart('v');

                string assetName = GetLauncherAssetName();
                return $"{LAUNCHER_DOWNLOAD_URL}/{version}/{assetName}";
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

        private static ProcessStartInfo PrepareLauncherStartInfo(string launcherPath)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
            };

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    startInfo.FileName = launcherPath;
                    return startInfo;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    startInfo.FileName = "open";
                    startInfo.Arguments = $"-n \"{launcherPath}\"";
                    return startInfo;
                default:
                    ReportHub.LogError(ReportCategory.UNSPECIFIED, "Unsupported platform for launching the application.");
                    return startInfo;
            }
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
                    ReportHub.LogError(ReportCategory.UNSPECIFIED, "Unsupported platform for launching the application.");
                    return null;
            }

            return possiblePaths.FirstOrDefault(path => File.Exists(path)
                                                        || (Directory.Exists(path) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)));
        }

        [Serializable]
        private struct GitHubRelease
        {
            public string tag_name;
        }
    }
}
