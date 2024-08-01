using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public class URLHealthCheck : IHealthCheck
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly DecentralandUrl url;

        public URLHealthCheck(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource, DecentralandUrl url)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.url = url;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            string? result = await webRequestController.HeadAsync(new CommonArguments(Url()), ct).StoreTextAsync();
        }

        private URLAddress Url()
        {
            string stringUrl = decentralandUrlsSource.Url(url);
            return URLAddress.FromString(stringUrl);
        }
    }
}
