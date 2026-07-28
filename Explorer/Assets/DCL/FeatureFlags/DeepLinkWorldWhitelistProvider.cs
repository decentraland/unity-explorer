using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.FeatureFlags
{
    /// <summary>
    ///     Best-effort, anonymous, standalone fetch of ONLY the deep-link world whitelist from the feature-flags
    ///     endpoint. Run early in bootstrap — before the full feature-flags system is initialized and before the
    ///     cold-start deep-link parse — so the deep-link allowlist can trust configured worlds at launch time
    ///     (feature flags are otherwise not available until after the parse).
    ///     <para>
    ///     Fails safe: any error, non-success response, or timeout yields an empty list (loopback-only).
    ///     </para>
    /// </summary>
    public static class DeepLinkWorldWhitelistProvider
    {
        private static readonly char[] SEPARATORS = { ',', '\n', '\r', ';' };

        /// <summary>
        ///     Fetches and parses only the whitelisted-worlds flag. <paramref name="fetchUrl" /> is the full
        ///     feature-flags document URL (e.g. https://feature-flags.decentraland.org/explorer.json).
        /// </summary>
        public static async UniTask<IReadOnlyList<string>> FetchAsync(string fetchUrl, CancellationToken ct)
        {
            try
            {
                // At this moment IWebRequestController can't be injected so perform a "raw" unity request
                using UnityWebRequest request = UnityWebRequest.Get(fetchUrl);
                request.SetRequestHeader("X-Debug", "false");

                // A start-up path can't wait for long time
                request.timeout = 5000;

                await request.SendWebRequest().WithCancellation(ct);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"Deep-link world whitelist fetch failed ({request.result}): {request.error}");
                    return Array.Empty<string>();
                }

                FeatureFlagsResultDto dto = JsonConvert.DeserializeObject<FeatureFlagsResultDto>(request.downloadHandler.text);
                dto = HttpFeatureFlagsProvider.StripAppNameFromKeys(FeatureFlagOptions.APP_NAME, dto);

                return ReadWorlds(new FeatureFlagsConfiguration(dto));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"Deep-link world whitelist fetch errored, falling back to loopback-only: {e.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        ///     Reads the whitelisted worlds from an already-initialized configuration (keys already app-name-stripped).
        ///     Used to refresh the allowlist once the full feature-flags system has loaded.
        /// </summary>
        public static IReadOnlyList<string> ReadWorlds(FeatureFlagsConfiguration configuration)
        {
            if (!configuration.TryGetPayload(FeatureFlagsStrings.DEEPLINK_WHITELISTED_WORLDS, out FeatureFlagPayload payload)
                || string.IsNullOrWhiteSpace(payload.value))
                return Array.Empty<string>();

            var worlds = new List<string>();

            // The payload lists world names (full or short form) separated by commas or newlines. Normalization to
            // the canonical world name is done by DeepLinkAllowlist, so entries are passed through verbatim here.
            foreach (string entry in payload.value.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = entry.Trim();

                if (trimmed.Length > 0)
                    worlds.Add(trimmed);
            }

            return worlds;
        }
    }
}
