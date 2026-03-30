using Cysharp.Threading.Tasks;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.Analytics.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace DCL.WebRequests.Dumper
{
    public class WebRequestDumpAnalyticsHandler : IWebRequestAnalyticsHandler
    {
        private readonly Dictionary<UnityWebRequest, WebRequestDump.Envelope> dumpEnvelopes = new (100);
        private readonly DebugMetricsAnalyticsHandler debugMetricsAnalyticsHandler;

        private readonly List<RequestMetricBase> flatMetrics = new ();

        public WebRequestDumpAnalyticsHandler(DebugMetricsAnalyticsHandler debugMetricsAnalyticsHandler)
        {
            this.debugMetricsAnalyticsHandler = debugMetricsAnalyticsHandler;
        }

        /// <summary>
        ///     Allows adding metrics dynamically without adding to the debug menu
        /// </summary>
        private void AddFlatMetric(RequestMetricBase metric)
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

        /// <summary>
        ///     Args can be become unavailable after WR execution so pre-processing required
        /// </summary>
        private static object PrepareArgsForSerialization<TWebRequestArgs>(TWebRequestArgs args) where TWebRequestArgs: struct
        {
            if (args is GenericPostArguments { UploadHandler: not null } postArguments)

                // BufferedStringUploadHandler is disposed of after the request
                return GenericPostArguments.Create(postArguments.UploadHandler.Value.ToString(), postArguments.ContentType!);

            return args;
        }

        private void RecreateMetricsIfNeeded()
        {
            if (WebRequestsDumper.Instance.activeMetrics.All(a => a == null))
                CreateAnalytics();
        }

        private void CreateAnalytics()
        {
            IDictionary<Type, Func<RequestMetricBase>> trackedMetrics = debugMetricsAnalyticsHandler.GetTrackedMetrics();

            foreach ((Type type, Func<RequestMetricBase> ctor) in trackedMetrics)
            {
                var recorder = new RequestMetricRecorder(ctor());
                WebRequestsDumper.Instance.activeMetrics[MetricsRegistry.INDICES[type]] = recorder;
                AddFlatMetric(recorder);
            }
        }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            WebRequestsDumper instance = WebRequestsDumper.Instance;

            // Signed requests are not supported
            if (instance.IsMatch(envelope.signInfo != null, envelope.CommonArguments.URL))
            {
                // Make sure the analytics are created at this point
                // They will be re-created in case of the domain reload
                RecreateMetricsIfNeeded();

                var dumpEnvelope = new WebRequestDump.Envelope(typeof(T), envelope.CommonArguments, typeof(TWebRequestArgs),
                    PrepareArgsForSerialization(envelope.args), envelope.headersInfo, startedAt, request.UnityWebRequest.url);

                instance.Add(dumpEnvelope);
                dumpEnvelopes[request.UnityWebRequest] = dumpEnvelope;
            }

            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestStarted(request, startedAt);
        }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestEnded(request, duration);

            if (dumpEnvelopes.Remove(request.UnityWebRequest, out WebRequestDump.Envelope? dumpEnvelope))
                dumpEnvelope.Conclude(WebRequestDump.Envelope.StatusKind.SUCCESS, DateTime.Now);
        }

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest { }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (dumpEnvelopes.Remove(request.UnityWebRequest, out WebRequestDump.Envelope? dumpEnvelope))
                dumpEnvelope.Conclude(WebRequestDump.Envelope.StatusKind.FAILURE, DateTime.Now);
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (dumpEnvelopes.Remove(request.UnityWebRequest, out WebRequestDump.Envelope? dumpEnvelope))
                dumpEnvelope.Conclude(WebRequestDump.Envelope.StatusKind.FAILURE, DateTime.Now);
        }
    }
}
