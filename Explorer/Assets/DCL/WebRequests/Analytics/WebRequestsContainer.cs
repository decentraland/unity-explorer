using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;

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

            var webRequestController = new LogWebRequestController(new WebRequestController(analyticsContainer, web3IdentityProvider));
            return new WebRequestsContainer { WebRequestController = webRequestController, AnalyticsContainer = analyticsContainer };
        }
    }
}
