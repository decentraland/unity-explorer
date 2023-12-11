using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, List<IRequestMetric>> ongoingRequests = new ();
        private readonly List<object> requestMetricTypes = new ();
        public IReadOnlyList<IRequestMetric> GetMetric(Type requestType) =>
            ongoingRequests.TryGetValue(requestType, out List<IRequestMetric> bucket) ? bucket : null;
        public void AddMetric<T>() where T: IRequestMetric
        {
            requestMetricTypes.Add(typeof(T));
        }

        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request)
        {
            if (!ongoingRequests.TryGetValue(typeof(T), out List<IRequestMetric> metrics))
            {
                metrics = new List<IRequestMetric>();

                foreach (object metricType in requestMetricTypes)
                {
                    metrics.Add((IRequestMetric)Activator.CreateInstance(metricType.GetType()));
                }

                ongoingRequests.Add(typeof(T), metrics);
            }

            foreach (var metric in metrics)
            {
                metric.OnRequestStarted(request);
            }
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (!ongoingRequests.TryGetValue(typeof(T), out List<IRequestMetric> metrics)) return;

            foreach (var metric in metrics)
            {
                metric.OnRequestEnded(request);
            }
        }
    }
}
