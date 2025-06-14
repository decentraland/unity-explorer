using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        IReadOnlyList<MetricRegistration> GetTrackedMetrics();

        IReadOnlyList<MetricRegistration.AggregatedMetric> GetAggregatedMetrics();

        internal void OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest);

        internal void OnRequestFinished(ITypedWebRequest request, IWebRequest webRequest);

        public static readonly IWebRequestsAnalyticsContainer DEFAULT = new WebRequestsAnalyticsContainer();
    }
}
