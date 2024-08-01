using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;

namespace DCL.Multiplayer.HealthChecks
{
    public class URLHealthCheck : IHealthCheck
    {
        private readonly IWebRequestController webRequestController;
        private readonly DecentralandUrl url;

        public URLHealthCheck(IWebRequestController webRequestController, DecentralandUrl url)
        {
            this.webRequestController = webRequestController;
            this.url = url;
        }

        public UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync()
        {
            return new UniTask<(bool success, string? errorMessage)>();
        }
    }
}
