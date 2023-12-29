using DCL.WebRequests.Analytics.Metrics;
using DCL.Web3Authentication;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; private set; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; private set; }

        public static WebRequestsContainer Create(IWeb3IdentityProvider web3IdentityProvider)
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
