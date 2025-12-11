using Cysharp.Threading.Tasks;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.WebRequests.Dumper
{
    public class WebRequestDumpRecorder : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly WebRequestsAnalyticsContainer analyticsContainer;

        IRequestHub IWebRequestController.RequestHub => origin.RequestHub;

        public WebRequestDumpRecorder(IWebRequestController origin, WebRequestsAnalyticsContainer analyticsContainer)
        {
            this.origin = origin;
            this.analyticsContainer = analyticsContainer;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            WebRequestsDumper instance = WebRequestsDumper.Instance;

            WebRequestDump.Envelope? dumpEnvelope = null;

            try
            {
                // Signed requests are not supported
                if (instance.IsMatch(envelope.signInfo != null, envelope.CommonArguments.URL))
                {
                    // Make sure the analytics are created at this point
                    // They will be re-created in case of the domain reload
                    RecreateMetricsIfNeeded();
                    instance.Add(dumpEnvelope = new WebRequestDump.Envelope(typeof(TWebRequest), envelope.CommonArguments, typeof(TWebRequestArgs), envelope.args, envelope.headersInfo, DateTime.Now));
                }

                TResult? result = await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);

                dumpEnvelope?.Conclude(WebRequestDump.Envelope.StatusKind.SUCCESS, DateTime.Now);
                return result;
            }
            catch (Exception)
            {
                dumpEnvelope?.Conclude(WebRequestDump.Envelope.StatusKind.FAILURE, DateTime.Now);
                throw;
            }
        }

        private void RecreateMetricsIfNeeded()
        {
            if (WebRequestsDumper.Instance.activeMetrics.All(a => a == null))
                CreateAnalytics();
        }

        private void CreateAnalytics()
        {
            IDictionary<Type, Func<RequestMetricBase>> trackedMetrics = analyticsContainer.GetTrackedMetrics();

            foreach ((Type type, Func<RequestMetricBase> ctor) in trackedMetrics)
            {
                var recorder = new RequestMetricRecorder(ctor());
                WebRequestsDumper.Instance.activeMetrics[MetricsRegistry.INDICES[type]] = recorder;
                analyticsContainer.AddFlatMetric(recorder);
            }
        }
    }
}
