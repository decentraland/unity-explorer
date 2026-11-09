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
    ///     Best-effort, anonymous, standalone fetch of the feature-flags document for decisions made early in bootstrap,
    ///     before the full feature-flags system is initialized (the deep-link world whitelist, local asset bundles).
    ///     <para>
    ///     Fails closed: any error, non-success response, or timeout yields null.
    ///     </para>
    /// </summary>
    public static class StartupFeatureFlagsFetcher
    {
        // A start-up path can't wait for long: UnityWebRequest.timeout is in seconds.
        private const int FETCH_TIMEOUT_SECONDS = 5;

        /// <summary>
        ///     <paramref name="fetchUrl" /> is the full feature-flags document URL
        ///     (e.g. https://feature-flags.decentraland.org/explorer.json). The returned keys are app-name-stripped.
        /// </summary>
        public static async UniTask<FeatureFlagsConfiguration?> FetchAsync(string fetchUrl, CancellationToken ct)
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
                    ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"Startup feature flags fetch failed ({request.result}): {request.error}");
                    return null;
                }

                FeatureFlagsResultDto dto = JsonConvert.DeserializeObject<FeatureFlagsResultDto>(request.downloadHandler.text);
                dto = HttpFeatureFlagsProvider.StripAppNameFromKeys(FeatureFlagOptions.APP_NAME, dto);

                return new FeatureFlagsConfiguration(dto);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.FEATURE_FLAGS, $"Startup feature flags fetch errored: {e.Message}");
                return null;
            }
        }
    }
}
