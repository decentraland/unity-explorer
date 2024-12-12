using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks
{
    public class URLHealthCheck : IHealthCheck
    {
        /// <summary>
        ///     Retries should be handles above with RetriesHealthCheck
        /// </summary>
        private const int ATTEMPTS = 1;

        private static readonly HashSet<int> ERROR_CODES = new ()
        {
            404,
            500,
        };
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly DecentralandUrl url;

        public URLHealthCheck(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource, DecentralandUrl url)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.url = url;
        }

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            URLAddress urlAddress = Url();

            try
            {
                int code = await webRequestController.HeadAsync(new CommonArguments(urlAddress, attemptsCount: ATTEMPTS), ct, ReportCategory.LIVEKIT).StatusCodeAsync();
                bool success = ERROR_CODES.Contains(code) == false;
                return success ? Result.SuccessResult() : Result.ErrorResult($"Cannot connect to {urlAddress}");
            }
            catch (Exception) { return Result.ErrorResult($"Cannot connect to {urlAddress}"); }
        }

        private URLAddress Url()
        {
            string stringUrl = decentralandUrlsSource.Url(url);
            return URLAddress.FromString(stringUrl);
        }
    }
}
