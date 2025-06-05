using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

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

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            Uri urlAddress = Url();
            return await webRequestController.IsHeadReachableAsync(ReportCategory.LIVEKIT, urlAddress, ct);
        }

        private Uri Url() =>
            decentralandUrlsSource.Url(url);
    }
}
