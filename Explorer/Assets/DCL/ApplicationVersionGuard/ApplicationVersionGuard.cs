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
        private const string API_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
        private const string LAUNCHER_API_URL = "https://api.github.com/repos/decentraland/launcher/releases/latest";

        private const string LAUNCHER_EXECUTABLE_NAME = "Decentraland Launcher";
        private const string LAUNCHER_PATH_MAC = "/Applications/" + LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string LAUNCHER_PATH_WIN_MAIN = @"C:\Program Files\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_86 = @"C:\Program Files (x86)\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_COMBINED = @"Programs\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";

        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;

        public ApplicationVersionGuard(IWebRequestController webRequestController, IWebBrowser webBrowser)
        {
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
        }

        public async UniTask<(string current, string latest)> GetVersionsAsync(CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(API_URL, new FlatFetchResponse<GenericGetRequest>(), ct);

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string latestVersion = latestRelease.tag_name.TrimStart('v');

            return (Application.version, latestVersion);
        }

        public async UniTask LaunchOrDownloadLauncherAsync(CancellationToken ct = default)
        {
            string? launcherPath = GetLauncherPath();

            if (string.IsNullOrEmpty(launcherPath))
                await DownloadAndRunLauncherAsync(ct);
            else
                RunLauncherAndQuit(launcherPath);
        }

        private async UniTask DownloadAndRunLauncherAsync(CancellationToken ct)
        {
            string downloadUrl = await GetLauncherDownloadUrlAsync(ct);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Debug.LogError("Failed to get launcher download URL.");
                return;
            }

            webBrowser.OpenUrl(downloadUrl);

            Debug.Log($"Downloading launcher from: {downloadUrl}");

            // You might want to show a message to the user here
        }

        private async UniTask<string> GetLauncherDownloadUrlAsync(CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(LAUNCHER_API_URL, new FlatFetchResponse<GenericGetRequest>(), ct);

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string version = latestRelease.tag_name.TrimStart('v');

            string assetName = GetLauncherAssetName();
            return $"https://github.com/decentraland/launcher/releases/download/{version}/{assetName}";
        }

        private static string GetLauncherAssetName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return "Decentraland-Launcher-win-x64.exe";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return SystemInfo.processorType.ToLower().Contains("arm")
                        ? "Decentraland-Launcher-mac-arm64.dmg"
                        : "Decentraland-Launcher-mac-x64.dmg";
                default:
                    throw new NotSupportedException("Unsupported platform for launcher download.");
            }
        }

        private static void RunLauncherAndQuit(string launcherPath)
        {
            try
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
                        break;
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
                        startInfo.FileName = "open";
                        startInfo.Arguments = $"-n \"{launcherPath}\"";
                        break;
                    default:
                        ReportHub.LogError(ReportCategory.UNSPECIFIED, "Unsupported platform for launching the application.");
                        return;
                }

                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException)
                    ReportHub.LogException(e, ReportCategory.UNSPECIFIED);
            }
            finally
            {
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
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
                    Debug.LogError("Unsupported platform for launching the application.");
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
