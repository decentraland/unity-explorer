using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.HealthChecks.Struct;
using DCL.WebRequests;
using System.Linq;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks
{
    public class MultipleURLHealthCheck : IHealthCheck
    {
        private readonly IHealthCheck healthCheck;

        public MultipleURLHealthCheck(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            params DecentralandUrl[] urls
        )
        {
            healthCheck = new ParallelHealthCheck(
                urls
                   .Select(e => new URLHealthCheck(webRequestController, decentralandUrlsSource, e) as IHealthCheck)
                   .ToArray()
            );
        }

        public UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct) =>
            healthCheck.IsRemoteAvailableAsync(ct);
    }
}
