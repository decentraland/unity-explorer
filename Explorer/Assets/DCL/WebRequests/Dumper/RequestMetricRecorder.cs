using DCL.DebugUtilities;
using DCL.WebRequests.Analytics;

namespace DCL.WebRequests.Dumper
{
    public class RequestMetricRecorder : IRequestMetric
    {
        private readonly IRequestMetric metric;

        public RequestMetricRecorder(IRequestMetric metric)
        {
            this.metric = metric;
        }

        public DebugLongMarkerDef.Unit GetUnit() =>
            metric.GetUnit();

        public ulong GetMetric() =>
            metric.GetMetric();

        private static bool IsSigned(ITypedWebRequest request) =>
            !string.IsNullOrEmpty(request.UnityWebRequest.GetRequestHeader("x-identity-auth-chain-0"));

        public void OnRequestStarted(ITypedWebRequest request)
        {
            if (!WebRequestsDumper.Instance.IsMatch(IsSigned(request), request.UnityWebRequest.url)) return;
            metric.OnRequestStarted(request);
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            if (!WebRequestsDumper.Instance.IsMatch(IsSigned(request), request.UnityWebRequest.url)) return;
            metric.OnRequestEnded(request);
        }
    }
}
