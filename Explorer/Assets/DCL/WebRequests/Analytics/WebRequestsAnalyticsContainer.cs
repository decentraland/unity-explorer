using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, List<IRequestMetric>> requestTypesWithMetrics = new ();
        private readonly Dictionary<Type, Func<IRequestMetric>> requestMetricTypes = new ();

        public IReadOnlyList<IRequestMetric>? GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);

        void IWebRequestsAnalyticsContainer.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest, IWebRequestAnalytics webRequestAnalytics)
        {
            Type type = request.GetType();

            if (!requestTypesWithMetrics.TryGetValue(type, out List<IRequestMetric> metrics))
            {
                metrics = new List<IRequestMetric>();

                foreach ((_, Func<IRequestMetric> ctor) in requestMetricTypes)
                    metrics.Add(ctor());

                requestTypesWithMetrics.Add(type, metrics);
            }

            foreach (IRequestMetric? metric in metrics) { metric.OnRequestStarted(request, webRequestAnalytics, webRequest); }
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished(ITypedWebRequest request, IWebRequest webRequest, IWebRequestAnalytics webRequestAnalytics)
        {
            Type type = request.GetType();

            if (!requestTypesWithMetrics.TryGetValue(type, out List<IRequestMetric> metrics)) return;

            foreach (IRequestMetric? metric in metrics) { metric.OnRequestEnded(request, webRequestAnalytics, webRequest); }
        }

        public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() =>
            requestMetricTypes;

        public WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: class, IRequestMetric, new()
        {
            requestMetricTypes.Add(typeof(T), () => new T());
            return this;
        }
    }
}
