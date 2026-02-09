using Cysharp.Threading.Tasks;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.Analytics.Metrics;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Dumper
{
    public class WebRequestDumpAnalyticsHandler : IWebRequestAnalyticsHandler
    {
        private readonly List<RequestMetricBase> flatMetrics = new ();

        /// <summary>
        ///     Allows adding metrics dynamically without adding to the debug menu
        /// </summary>
        public void AddFlatMetric(RequestMetricBase metric)
        {
            flatMetrics.Add(metric);
        }

        public void RemoveFlatMetric(RequestMetricBase metric) =>
            flatMetrics.Remove(metric);

        public void Update(float dt)
        {
            foreach (RequestMetricBase requestMetricBase in flatMetrics)
                requestMetricBase.Update();
        }

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct { }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestStarted(request, startedAt);
        }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestEnded(request, duration);
        }

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest { }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            OnRequestFinished(request, duration);
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest
        {
            OnRequestFinished(request, duration);
        }
    }
}
