using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.ApplicationBlocklistGuard
{
    public static class ApplicationBlocklistGuard
    {
        public static async UniTask<GetBanStatusData> IsUserBlocklistedAsync(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, string userID, ModerationDataProvider moderationDataProvider, CancellationToken ct)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.REPORT_USER))
            {
                var result = await moderationDataProvider.GetBanStatusAsync(userID, ct)
                                                         .SuppressToResultAsync(ReportCategory.STARTUP);

                if (!result.Success)
                {
                    ReportHub.LogError(ReportCategory.STARTUP, $"Failed to get ban status: {result.ErrorMessage}. Skipping blocklist check.");
                    return new GetBanStatusData { isBanned = false };
                }

                return result.Value.data;
            }

            try
            {
                FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                    urlsSource.Url(DecentralandUrl.Blocklist),
                    new FlatFetchResponse<GenericGetRequest>(),
                    ct,
                    ReportCategory.STARTUP,
                    new WebRequestHeadersInfo());

                BlocklistData bd = JsonUtility.FromJson<BlocklistData>(response.body);

                foreach (var t in bd.users)
                {
                    if (string.Equals(t.wallet, userID, StringComparison.OrdinalIgnoreCase))
                        return new GetBanStatusData { isBanned = true };
                }

                return new GetBanStatusData { isBanned = false };
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.STARTUP, $"Failed to parse blocklist JSON: {ex.Message}. Skipping blocklist check.");
                return new GetBanStatusData { isBanned = false };
            }
        }
    }
}
