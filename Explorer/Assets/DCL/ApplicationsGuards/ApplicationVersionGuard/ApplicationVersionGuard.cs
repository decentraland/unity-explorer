using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.ApplicationVersionGuard
{
    public class ApplicationVersionGuard
    {
        private const string LAUNCHER_EXECUTABLE_NAME = "Decentraland";
        private const string LEGACY_LAUNCHER_EXECUTABLE_NAME = "Decentraland Launcher";
        private const string LAUNCHER_EXECUTABLE_FILENAME = "dcl_launcher.exe";
        private const string LAUNCHER_PATH_MAC = "/Applications/" + LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string LEGACY_LAUNCHER_PATH_MAC = "/Applications/" + LEGACY_LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string DECENTRALAND_LAUNCHER_WIN_X64_EXE = "Decentraland_x64-setup.exe";
        private const string DECENTRALAND_LAUNCHER_MAC_ARM_64DMG = "Decentraland_aarch64.dmg";
        //Aga: Rust version of launcher does not support intel macs, until fully deprecating it, we need to keep the old launcher for intel based macs
        private const string DECENTRALAND_LEGACY_LAUNCHER_MAC_X_64DMG = "Decentraland Launcher-mac-x64.dmg";

        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;

        public ApplicationVersionGuard(IWebRequestController webRequestController, IWebBrowser webBrowser)
        {
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
        }

        public async UniTask<string> GetLatestVersionAsync(CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                IDecentralandUrlsSource.EXPLORER_LATEST_RELEASE_URL,
                new FlatFetchResponse<GenericGetRequest>(),
                ct,
                ReportCategory.VERSION_CONTROL,
                new WebRequestHeadersInfo());

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string latestVersion = latestRelease.tag_name.TrimStart('v');

            return latestVersion;
        }

        public async UniTask LaunchOrDownloadLauncherAsync(CancellationToken ct = default)
        {
            string? launcherPath = GetLauncherPath();

            if (string.IsNullOrEmpty(launcherPath))
            {
                DownloadLauncher();
                ExitUtils.Exit();
            }
            else
            {
                try
                {
                    await UniTask.Delay(1000, cancellationToken: ct);
                    PlatformUtils.ShellExecute(launcherPath);
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                        ReportHub.LogException(e, ReportCategory.VERSION_CONTROL);
                }
                finally
                {
                    await UniTask.Delay(2000, cancellationToken: ct);
                    ExitUtils.Exit();
                }
            }
        }

        private void DownloadLauncher()
        {
            string assetName = GetLauncherAssetName();
            string downloadUrl = $"{GetLauncherDownloadPath()}/{assetName}";

            if (!string.IsNullOrEmpty(downloadUrl))
                webBrowser.OpenUrl(downloadUrl);
            else
                ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Failed to get launcher download URL.");
        }

        private static string GetLauncherAssetName()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => DECENTRALAND_LAUNCHER_WIN_X64_EXE,
                RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => IsAppleSiliconMac ? DECENTRALAND_LAUNCHER_MAC_ARM_64DMG : DECENTRALAND_LEGACY_LAUNCHER_MAC_X_64DMG,
                _ => throw new NotSupportedException("Unsupported platform for launcher download."),
            };
        }


        private static string GetLauncherDownloadPath()
        {
            return Application.platform switch
                   {
                       RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => IDecentralandUrlsSource.LAUNCHER_DOWNLOAD_URL,
                       RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => IsAppleSiliconMac ? IDecentralandUrlsSource.LAUNCHER_DOWNLOAD_URL : IDecentralandUrlsSource.LEGACY_LAUNCHER_DOWNLOAD_URL,
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
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LAUNCHER_EXECUTABLE_NAME, LAUNCHER_EXECUTABLE_FILENAME),
                    };
                    break;

                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    possiblePaths = IsAppleSiliconMac
                        ? new[]
                        {
                            LAUNCHER_PATH_MAC,
                            $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}{LAUNCHER_PATH_MAC}",
                        }
                        : new[]
                        {
                            LEGACY_LAUNCHER_PATH_MAC,
                            $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}{LEGACY_LAUNCHER_PATH_MAC}",
                        };
                    break;

                default:
                    ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Unsupported platform for launching the application.");
                    return null;
            }

            return possiblePaths.FirstOrDefault(path =>
                File.Exists(path) ||
                (Directory.Exists(path) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)));
        }

        private static bool IsAppleSiliconMac =>
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer &&
            SystemInfo.processorType.Contains("apple", StringComparison.OrdinalIgnoreCase);

        [Serializable]
        private struct GitHubRelease
        {
            public string tag_name;
        }
    }
}
