using DCL.WebRequests.Analytics;
using DCL.WebRequests.Analytics.Metrics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.PerformanceTesting;
using UnityEngine.Networking;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    public class PerformanceTestWebRequestsAnalytics : IWebRequestsAnalyticsContainer
    {
        private readonly struct RequestState
        {
            internal readonly long started;

            public RequestState(long started)
            {
                this.started = started;
            }
        }

        internal const string SEND_REQUEST_MARKER = "WebRequest.Send";
        internal const string PROCESS_DATA_MARKER = "WebRequest.ProcessData";

        internal readonly SampleGroup sendRequest = new (SEND_REQUEST_MARKER, SampleUnit.Microsecond);
        internal readonly SampleGroup processData = new (PROCESS_DATA_MARKER, SampleUnit.Microsecond);

        private readonly Dictionary<UnityWebRequest, RequestState> requests = new ();

        public bool WarmingUp { private get; set; }

        public IDictionary<Type, Func<RequestMetricBase>> GetTrackedMetrics() =>
            new Dictionary<Type, Func<RequestMetricBase>>();

        public IReadOnlyList<RequestMetricBase> GetMetric(Type requestType) =>
            Array.Empty<RequestMetricBase>();

        public static double ToMs(long a, long b) =>
            (b - a) * (1_000_000.0 / Stopwatch.Frequency);

        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request)
        {
            if (WarmingUp) return;

            requests[request.UnityWebRequest] = new RequestState(Stopwatch.GetTimestamp());
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (WarmingUp) return;

            Measure.Custom(sendRequest, ToMs(requests[request.UnityWebRequest].started, Stopwatch.GetTimestamp()));
            requests.Remove(request.UnityWebRequest);
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataStarted<T>(T request)
        {
            if (WarmingUp) return;

            requests[request.UnityWebRequest] = new RequestState(Stopwatch.GetTimestamp());
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request)
        {
            if (WarmingUp) return;

            Measure.Custom(processData, ToMs(requests[request.UnityWebRequest].started, Stopwatch.GetTimestamp()));
            requests.Remove(request.UnityWebRequest);
        }
    }
}
