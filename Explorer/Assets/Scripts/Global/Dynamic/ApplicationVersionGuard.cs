using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    public class ApplicationVersionGuard
    {
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

        [Serializable]
        private struct GitHubRelease
        {
            public string tag_name;
        }
    }
}
