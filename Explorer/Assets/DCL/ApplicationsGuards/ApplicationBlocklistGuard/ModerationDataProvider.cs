using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;

namespace DCL.ApplicationBlocklistGuard
{
    public class ModerationDataProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public ModerationDataProvider(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<GetBanStatusResponse> GetBanStatusAsync(string userId, CancellationToken ct)
        {
            var url = string.Format(urlsSource.Url(DecentralandUrl.BannedUsers), userId);

            GetBanStatusResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                      .CreateFromJson<GetBanStatusResponse>(WRJsonParser.Newtonsoft);

            return response;
        }
    }
}
