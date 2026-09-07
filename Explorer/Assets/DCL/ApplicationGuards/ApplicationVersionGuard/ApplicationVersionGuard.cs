using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using DCL.WebRequests;
using Global.AppArgs;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

#if UNITY_EDITOR
// ReSharper disable once RedundantUsingDirective
using UnityEditor;
#endif

namespace DCL.ApplicationGuards
{
    public class ApplicationVersionGuard
    {
        private const string LAUNCHER_EXECUTABLE_NAME = "Decentraland";
        private const string LEGACY_LAUNCHER_EXECUTABLE_NAME = "Decentraland Launcher";
        private const string LAUNCHER_EXECUTABLE_FILENAME = "dcl_launcher.exe";
        private const string LAUNCHER_PATH_MAC = "/Applications/" + LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string LEGACY_LAUNCHER_PATH_MAC = "/Applications/" + LEGACY_LAUNCHER_EXECUTABLE_NAME + ".app";
        private const string DECENTRALAND_LAUNCHER_WIN_X64_EXE = "Decentraland_installer.exe";
        private const string DECENTRALAND_LAUNCHER_MAC_ARM_64_DMG = "Decentraland_installer.dmg";
        //Aga: Rust version of launcher does not support intel macs, until fully deprecating it, we need to keep the old launcher for intel based macs
        private const string DECENTRALAND_LEGACY_LAUNCHER_MAC_X_64_DMG = "Decentraland Outdated-mac-x64.dmg";

        // Linux ships without a launcher: the preview channel publishes its own latest.json
        // beside the artifact, and the update path is the download page itself.
        private const string LINUX_LATEST_RELEASE_URL = "https://interconnected.online/downloads/latest-linux.json";
        private const string LINUX_DOWNLOAD_PAGE_URL = "https://interconnected.online/download/linux";

        private readonly IWebRequestController webRequestController;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IAppArgs appArgs;

        public ApplicationVersionGuard(IWebRequestController webRequestController, UnityAppWebBrowser webBrowser, IAppArgs appArgs)
        {
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
            this.appArgs = appArgs;
        }

        /// <summary>
        ///     True when a launcher owns the process: launchers identify themselves on the command line through
        ///     their analytics id and their version; a hand-run player carries neither.
        /// </summary>
        public static bool IsLauncherOwned(IAppArgs appArgs) =>
            appArgs.HasFlag(AppArgsFlags.Analytics.LAUNCHER_ID) || appArgs.HasFlag(AppArgsFlags.Launcher.VERSION);

        public async UniTask<string> GetLatestVersionAsync(CancellationToken ct)
        {
            if (isLinux)
            {
                try
                {
                    string latestVersion = await FetchLatestVersionAsync(LINUX_LATEST_RELEASE_URL, ct);
                    ReportHub.LogProductionInfo($"[VersionGuard] Running version {Application.version}: {LINUX_LATEST_RELEASE_URL} publishes {latestVersion}");
                    return latestVersion;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // An unreachable update channel is not a reason to block a preview boot.
                    ReportHub.LogException(e, ReportCategory.VERSION_CONTROL);
                    ReportHub.LogProductionInfo($"[VersionGuard] Release manifest {LINUX_LATEST_RELEASE_URL} could not be read, continuing with version {Application.version} unchecked");
                    return "0.0.0";
                }
            }

            return await FetchLatestVersionAsync(IDecentralandUrlsSource.EXPLORER_LATEST_RELEASE_URL, ct);
        }

        private async UniTask<string> FetchLatestVersionAsync(string releaseUrl, CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                releaseUrl,
                new FlatFetchResponse<GenericGetRequest>(),
                ct,
                ReportCategory.VERSION_CONTROL,
                new WebRequestHeadersInfo());

            ClientVersionInfo versionInfo = JsonUtility.FromJson<ClientVersionInfo>(response.body);
            string latestVersion = versionInfo.version.TrimStart('v');

            return latestVersion;
        }

        public async UniTask LaunchOrDownloadLauncherAsync(CancellationToken ct = default)
        {
            if (isLinux)
            {
                // A launcher-owned process only has to end: the launcher re-resolves the download itself.
                if (IsLauncherOwned(appArgs))
                {
                    ReportHub.LogProductionInfo("[VersionGuard] Exiting so the owning launcher fetches the update");
                    ExitUtils.Exit();
                    return;
                }

                // Without a launcher the download page is the whole update path.
                webBrowser.OpenUrlMainThreadOnly(LINUX_DOWNLOAD_PAGE_URL);
                await UniTask.Delay(2000, cancellationToken: ct);
                ExitUtils.Exit();
                return;
            }

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
                    Utility.PlatformUtils.ShellExecute(launcherPath);
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
            if (Application.platform is RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor)
            {
                // Fork-specific preview channel URL; upstream ships no Linux launcher asset yet.
                webBrowser.OpenUrlMainThreadOnly("https://interconnected.online/download/linux");
                return;
            }

            string assetName = GetLauncherAssetName();
            string downloadUrl = $"{GetLauncherDownloadPath()}/{assetName}";

            if (!string.IsNullOrEmpty(downloadUrl))
                webBrowser.OpenUrlMainThreadOnly(downloadUrl);
            else
                ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Failed to get launcher download URL.");
        }

        private static string GetLauncherAssetName()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => DECENTRALAND_LAUNCHER_WIN_X64_EXE,
                RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => isAppleSiliconMac ? DECENTRALAND_LAUNCHER_MAC_ARM_64_DMG : DECENTRALAND_LEGACY_LAUNCHER_MAC_X_64_DMG,
                _ => throw new NotSupportedException("Unsupported platform for launcher download."),
            };
        }


        private static string GetLauncherDownloadPath()
        {
            return Application.platform switch
                   {
                       RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => IDecentralandUrlsSource.LAUNCHER_DOWNLOAD_URL,
                       RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => isAppleSiliconMac ? IDecentralandUrlsSource.LAUNCHER_DOWNLOAD_URL : IDecentralandUrlsSource.LEGACY_LAUNCHER_DOWNLOAD_URL,
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
                    possiblePaths = isAppleSiliconMac
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

                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return GetLinuxLauncherPath();

                default:
                    ReportHub.LogError(ReportCategory.VERSION_CONTROL, "Unsupported platform for launching the application.");
                    return null;
            }

            return possiblePaths.FirstOrDefault(path =>
                File.Exists(path) ||
                (Directory.Exists(path) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)));
        }

        private static bool isLinux =>
            Application.platform is RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor;

        private static string? GetLinuxLauncherPath()
        {
            // Explicit override for custom setups.
            string? envPath = Environment.GetEnvironmentVariable("DCL_LAUNCHER_BIN");

            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
                return envPath;

            // Fall back to the NixOS launcher wrapper name on PATH.
            string? pathVar = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrEmpty(pathVar))
                return null;

            foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(dir, "decentraland");

                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static bool isAppleSiliconMac =>
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer &&
            SystemInfo.processorType.Contains("apple", StringComparison.OrdinalIgnoreCase);

        [Serializable]
        private struct ClientVersionInfo
        {
            public string version;
            public string timestamp;
        }
    }
}
