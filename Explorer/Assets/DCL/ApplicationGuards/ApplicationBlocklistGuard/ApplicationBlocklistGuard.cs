using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.ApplicationBlocklistGuard
{
    public static class ApplicationBlocklistGuard
    {
        public static async UniTask<bool> IsUserBlocklistedAsync(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, string userID, CancellationToken ct)
        {
            FlatFetchResponse response = await webRequestController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                urlsSource.Url(DecentralandUrl.Blocklist),
                new FlatFetchResponse<GenericGetRequest>(),
                ct,
                ReportCategory.STARTUP,
                new WebRequestHeadersInfo());

            BlocklistData bd = JsonUtility.FromJson<BlocklistData>(response.body);

            return bd.users.Any(u => u.wallet == userID);
        }
    }
}
