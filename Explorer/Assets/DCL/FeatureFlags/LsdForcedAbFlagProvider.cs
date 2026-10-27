using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Threading;
using UnityEngine.Networking;
using Utility.Networking;

namespace DCL.FeatureFlags
{
    /// <summary>
    ///     Best-effort, anonymous, standalone read of the <see cref="FeatureFlagsStrings.LSD_FORCED_AB" /> flag.
    ///     Local scene development decides whether to launch the abgen sidecar before the full feature-flags system
    ///     is initialized, so the flag is fetched on its own at that point.
    ///     <para>
    ///     Fails closed: any error, non-success response, or timeout reads as disabled (raw GLTFs).
    ///     </para>
    /// </summary>
    public static class LsdForcedAbFlagProvider
    {
        // A start-up path can't wait for long: UnityWebRequest.timeout is in seconds.
        private const int FETCH_TIMEOUT_SECONDS = 5;

        /// <summary>
        ///     <paramref name="fetchUrl" /> is the full feature-flags document URL
        ///     (e.g. https://feature-flags.decentraland.org/explorer.json).
        /// </summary>
        public static async UniTask<bool> IsEnabledAsync(string fetchUrl, CancellationToken ct)
        {
            try
            {
                // At this moment IWebRequestController can't be injected so perform a "raw" unity request
                using UnityWebRequest request = UnityWebRequest.Get(fetchUrl);
                CertificateHandler? certificateHandler = LocalCertificateValidation.CreateCertificateHandler(request.url);
                if (certificateHandler != null)
                    request.certificateHandler = certificateHandler;

                request.SetRequestHeader("X-Debug", "false");

                request.timeout = FETCH_TIMEOUT_SECONDS;

                await request.SendWebRequest().WithCancellation(ct);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"{FeatureFlagsStrings.LSD_FORCED_AB} flag fetch failed ({request.result}): {request.error}");
                    return false;
                }

                FeatureFlagsResultDto dto = JsonConvert.DeserializeObject<FeatureFlagsResultDto>(request.downloadHandler.text);
                dto = HttpFeatureFlagsProvider.StripAppNameFromKeys(FeatureFlagOptions.APP_NAME, dto);

                return new FeatureFlagsConfiguration(dto).IsEnabled(FeatureFlagsStrings.LSD_FORCED_AB);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"{FeatureFlagsStrings.LSD_FORCED_AB} flag fetch errored, loading raw GLTFs: {e.Message}");
                return false;
            }
        }
    }
}
