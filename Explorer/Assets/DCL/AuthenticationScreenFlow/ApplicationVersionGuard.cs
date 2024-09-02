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
        private const string API_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";

        private const string LAUNCHER_EXECUTABLE_NAME = "Decentraland Launcher";

        private const string LAUNCHER_PATH_MAC = "/Applications/" + LAUNCHER_EXECUTABLE_NAME + ".app";

        private const string LAUNCHER_PATH_WIN_MAIN = @"C:\Program Files\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_86 = @"C:\Program Files (x86)\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";
        private const string LAUNCHER_PATH_WIN_COMBINED = @"Programs\Decentraland Launcher\" + LAUNCHER_EXECUTABLE_NAME + ".exe";

        public static async UniTask<(string current, string latest)> GetVersions(IWebRequestController webRequestController, CancellationToken ct)
        {
            var response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(API_URL, new FlatFetchResponse<GenericGetRequest>(), ct);

            GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
            string latestVersion = latestRelease.tag_name.TrimStart('v');

            return (Application.version, latestVersion);
        }

        public static bool IsOlderThan(this string current, string latest) =>
            current.ToSemanticVersion().IsOlderThan(latest.ToSemanticVersion());

        private static bool IsOlderThan(this (int, int, int) current, (int, int, int) latest)
        {
            if (current.Item1 < latest.Item1) return true;
            if (current.Item2 < latest.Item2) return true;
            return current.Item3 < latest.Item3;
        }

        private static (int, int, int) ToSemanticVersion(this string versionString)
        {
            Match match = Regex.Match(versionString, @"v?(\d+)\.?(\d*)\.?(\d*)");

            if (!match.Success) return (0, 0, 0); // Default if no version found

            var major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            return (major, minor, patch);
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

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    possiblePaths = new[]
                    {
                        LAUNCHER_PATH_WIN_MAIN,
                        LAUNCHER_PATH_WIN_86,
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LAUNCHER_PATH_WIN_COMBINED)
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
