using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public class URLHealthCheck : IHealthCheck
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly DecentralandUrl url;

        private static readonly HashSet<int> ERROR_CODES = new ()
        {
            404,
            500,
        };

        /// <summary>
        ///     Retries should be handles above with RetriesHealthCheck
        /// </summary>
        private const int ATTEMPTS = 1;

        public URLHealthCheck(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource, DecentralandUrl url)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.url = url;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            var urlAddress = Url();
            int code = await webRequestController.HeadAsync(new CommonArguments(urlAddress, attemptsCount: ATTEMPTS), ct).StatusCodeAsync();
            bool success = ERROR_CODES.Contains(code) == false;
            return (success, success ? null : $"Cannot connect to {urlAddress}");
        }

        private URLAddress Url()
        {
            string stringUrl = decentralandUrlsSource.Url(url);
            return URLAddress.FromString(stringUrl);
        }
    }
}
