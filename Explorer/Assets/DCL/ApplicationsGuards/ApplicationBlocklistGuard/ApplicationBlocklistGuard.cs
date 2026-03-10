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
        public static async UniTask<bool> IsUserBlocklistedAsync(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, string userID, ModerationDataProvider moderationDataProvider, CancellationToken ct)
        {
            try
            {
                if (FeaturesRegistry.Instance.IsEnabled(FeatureId.REPORT_USER))
                {
                    var result = await moderationDataProvider.GetBanStatusAsync(userID, ct)
                                                             .SuppressToResultAsync(ReportCategory.STARTUP);

                    return result is { Success: true, Value: { data: { isBanned: true } } };
                }

                FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                    urlsSource.Url(DecentralandUrl.Blocklist),
                    new FlatFetchResponse<GenericGetRequest>(),
                    ct,
                    ReportCategory.STARTUP,
                    new WebRequestHeadersInfo());

                BlocklistData bd = JsonUtility.FromJson<BlocklistData>(response.body);

                foreach (var t in bd.users)
                {
                    if (string.Equals(t.wallet, userID, StringComparison.OrdinalIgnoreCase)) return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.STARTUP, $"Failed to parse blocklist JSON: {ex.Message}. Skipping blocklist check.");
                return false;
            }
        }
    }
}
