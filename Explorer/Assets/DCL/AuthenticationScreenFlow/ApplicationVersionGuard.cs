using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Global.Dynamic
{
    public static class ApplicationVersionGuard
    {
        const string launcherPathWin = @"C:\Program Files\Decentraland Launcher\Decentraland Launcher.exe";
        const string launcherPathMac = "/Applications/Decentraland Launcher.app";

        public static async UniTask<bool> VersionIsOlder(IWebRequestController webRequestController, CancellationToken ct)
        {
            const string API_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
            var webController = webRequestController;
            var response = await webController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(API_URL, new FlatFetchResponse<GenericGetRequest>(), ct);

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string latestVersion = latestRelease.tag_name.TrimStart('v');

            return IsOlder(ExtractSemanticVersion(Application.version), ExtractSemanticVersion(latestVersion));
        }

        private static (int, int, int) ExtractSemanticVersion(string versionString)
        {
            Match match = Regex.Match(versionString, @"v?(\d+)\.?(\d*)\.?(\d*)");

            if (!match.Success) return (0, 0, 0); // Default if no version found

            var major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            return (major, minor, patch);
        }

        private static bool IsOlder((int, int, int) current, (int, int, int) latest)
        {
            if (current.Item1 < latest.Item1) return true;
            if (current.Item2 < latest.Item2) return true;
            return current.Item3 < latest.Item3;
        }

        public static void LaunchExternalAppAndQuit()
        {
            string? launcherPath = GetLauncherPath();

            if (string.IsNullOrEmpty(launcherPath))
            {
                Debug.LogError("Launcher path not found. Please check the installation or set the correct path.");
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
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
                        Debug.LogError("Unsupported platform for launching the application.");
                        return;
                }

                Process.Start(startInfo);

                // Quit the Unity application
                Application.Quit();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error launching external application: {e.Message}");
            }
        }

        private static string? GetLauncherPath()
        {
            string[] possiblePaths;

            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                possiblePaths = new[]
                {
                    launcherPathWin,
                    @"C:\Program Files\Decentraland Launcher\Decentraland Launcher.exe",
                    @"C:\Program Files (x86)\Decentraland Launcher\Decentraland Launcher.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Decentraland Launcher\Decentraland Launcher.exe")
                };
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                possiblePaths = new[]
                {
                    launcherPathMac,
                    "/Applications/Decentraland Launcher.app",
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}/Applications/Decentraland Launcher.app"
                };
            }
            else
            {
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
