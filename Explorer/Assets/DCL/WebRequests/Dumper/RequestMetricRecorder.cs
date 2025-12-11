using DCL.DebugUtilities;
using DCL.WebRequests.Analytics.Metrics;
using System;

namespace DCL.WebRequests.Dumper
{
    public class RequestMetricRecorder : RequestMetricBase
    {
        private readonly RequestMetricBase metric;

        public RequestMetricRecorder(RequestMetricBase metric)
        {
            this.metric = metric;
        }

        public override DebugLongMarkerDef.Unit GetUnit() =>
            metric.GetUnit();

        public override ulong GetMetric() =>
            metric.GetMetric();

        private static bool IsSigned(ITypedWebRequest request) =>
            !string.IsNullOrEmpty(request.UnityWebRequest.GetRequestHeader("x-identity-auth-chain-0"));

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
            if (!WebRequestsDumper.Instance.IsMatch(IsSigned(request), request.UnityWebRequest.url)) return;
            metric.OnRequestStarted(request, startTime);
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            if (!WebRequestsDumper.Instance.IsMatch(IsSigned(request), request.UnityWebRequest.url)) return;
            metric.OnRequestEnded(request, duration);
        }
    }
}
