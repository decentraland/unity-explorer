using DCL.WebRequests.Analytics.Metrics;
using DCL.Web3Authentication;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; private set; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; private set; }

        public static WebRequestsContainer Create(IWeb3Authenticator web3Authenticator)
        {
            var analyticsContainer = new WebRequestsAnalyticsContainer().AddTrackedMetric<ActiveCounter>()
                                                                        .AddTrackedMetric<Total>()
                                                                        .AddTrackedMetric<TotalFailed>()
                                                                        .AddTrackedMetric<BandwidthDown>()
                                                                        .AddTrackedMetric<BandwidthUp>();

            var webRequestController = new WebRequestController(analyticsContainer, web3Authenticator);
            return new WebRequestsContainer { WebRequestController = webRequestController, AnalyticsContainer = analyticsContainer };
        }
    }
}
