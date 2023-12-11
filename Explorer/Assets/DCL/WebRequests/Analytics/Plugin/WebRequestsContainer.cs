using DCL.WebRequests.Analytics.Metrics;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; private set; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; private set; }

        public static WebRequestsContainer Create()
        {
            var analyticsContainer = new WebRequestsAnalyticsContainer().AddTrackedMetric<ActiveCounter>()
                                                                        .AddTrackedMetric<BandwidthDown>()
                                                                        .AddTrackedMetric<BandwidthUp>();

            var webRequestController = new WebRequestController(analyticsContainer);
            return new WebRequestsContainer { WebRequestController = webRequestController, AnalyticsContainer = analyticsContainer };
        }
    }
}
