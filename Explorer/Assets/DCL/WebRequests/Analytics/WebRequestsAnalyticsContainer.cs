using DCL.WebRequests.Analytics.Metrics;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, List<IRequestMetric>> requestTypesWithMetrics = new ();
        private readonly Dictionary<Type, Func<IRequestMetric>> requestMetricTypes = new ();

        private readonly List<IRequestMetric> flatMetrics = new ();

        public WebRequestsAnalyticsContainer AddFlatMetric(IRequestMetric metric)
        {
            flatMetrics.Add(metric);
            return this;
        }

        public IReadOnlyList<IRequestMetric>? GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);

        public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() =>
            requestMetricTypes;

        public WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: class, IRequestMetric, new()
        {
            requestMetricTypes.Add(typeof(T), () => new T());

            // Allow adding metrics dynamically at runtime
            foreach ((_, List<IRequestMetric>? metrics) in requestTypesWithMetrics)
                metrics.Add(new T());

            return this;
        }

        public void RemoveFlatMetric(IRequestMetric metric) =>
            flatMetrics.Remove(metric);

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

            foreach (IRequestMetric flat in flatMetrics)
                flat.OnRequestStarted(request);
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<IRequestMetric> metrics)) return;

            foreach (var metric in metrics)
            {
                metric.OnRequestEnded(request);
            }

            foreach (IRequestMetric flat in flatMetrics)
                flat.OnRequestEnded(request);
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataStarted<T>(T request) { }

        void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request) { }
    }
}
