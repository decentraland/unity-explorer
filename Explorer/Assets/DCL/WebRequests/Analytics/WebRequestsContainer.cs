using DCL.WebRequests.Analytics.Metrics;
using DCL.Web3Authentication;
using DCL.Web3Authentication.Identities;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; private set; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; private set; }

        public static WebRequestsContainer Create(IWeb3IdentityCache web3IdentityProvider)
        {
            var analyticsContainer = new WebRequestsAnalyticsContainer().AddTrackedMetric<ActiveCounter>()
                                                                        .AddTrackedMetric<Total>()
                                                                        .AddTrackedMetric<TotalFailed>()
                                                                        .AddTrackedMetric<BandwidthDown>()
                                                                        .AddTrackedMetric<BandwidthUp>();

            var webRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider);
            return new WebRequestsContainer { WebRequestController = webRequestController, AnalyticsContainer = analyticsContainer };
        }
    }
}
