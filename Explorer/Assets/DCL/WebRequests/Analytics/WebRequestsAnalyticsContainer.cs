using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, List<IRequestMetric>> requestTypesWithMetrics = new ();
        private readonly List<Type> requestMetricTypes = new ();
        public IReadOnlyList<IRequestMetric> GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);

        public IReadOnlyList<Type> GetTrackedMetrics() => requestMetricTypes;

        public WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: IRequestMetric, new()
        {
            requestMetricTypes.Add(typeof(T));
            return this;
        }
        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request)
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<IRequestMetric> metrics))
            {
                metrics = new List<IRequestMetric>();

                foreach (Type metricType in requestMetricTypes)
                {
                    metrics.Add((IRequestMetric)Activator.CreateInstance(metricType));
                }

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
