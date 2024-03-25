using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IMutableWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, List<IRequestMetric>> requestTypesWithMetrics = new ();
        private readonly Dictionary<Type, Func<IRequestMetric>> requestMetricTypes = new ();

        public IReadOnlyList<IRequestMetric> GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);

        public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() =>
            requestMetricTypes;

        public WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: class, IRequestMetric, new()
        {
            requestMetricTypes.Add(typeof(T), () => new T());
            return this;
        }
        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request)
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<IRequestMetric> metrics))
            {
                metrics = new List<IRequestMetric>();

                foreach ((_, Func<IRequestMetric> ctor) in requestMetricTypes)
                    metrics.Add(ctor());

                requestTypesWithMetrics.Add(typeof(T), metrics);
            }

            foreach (var metric in metrics)
            {
                metric.OnRequestStarted(request);
            }
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<IRequestMetric> metrics)) return;

            foreach (var metric in metrics)
            {
                metric.OnRequestEnded(request);
            }
        }
    }
}
