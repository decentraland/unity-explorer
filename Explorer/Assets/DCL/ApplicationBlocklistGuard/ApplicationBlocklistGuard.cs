using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.ApplicationBlocklistGuard
{
    public static class ApplicationBlocklistGuard
    {
        public static async UniTask<bool> IsUserBlocklistedAsync(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, string userID, CancellationToken ct)
        {
            var bd = await webRequestController.GetAsync(
                urlsSource.Url(DecentralandUrl.Blocklist),
                ReportCategory.STARTUP,
                new WebRequestHeadersInfo())
                                               .CreateFromJsonAsync<BlocklistData>(WRJsonParser.Unity, ct);

            return bd.users.Any(u => u.wallet == userID);
        }
    }
}
