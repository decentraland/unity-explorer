using DCL.DebugUtilities;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class TotalFailed : IRequestMetric
    {
        private ulong counter { get; set; }

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.NoFormat;

        public ulong GetMetric() =>
            counter;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest) { }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
            if (webRequest.Response.IsSuccess) counter++;
        }
    }
}
