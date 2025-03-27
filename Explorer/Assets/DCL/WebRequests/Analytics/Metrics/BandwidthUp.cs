using DCL.DebugUtilities;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class BandwidthUp : IRequestMetric
    {
        private ulong bandwidth { get; set; }

        public DebugLongMarkerDef.Unit GetUnit() => DebugLongMarkerDef.Unit.Bytes;

        public ulong GetMetric() => bandwidth;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
            if (webRequest.Response.IsSuccess)
                bandwidth += webRequestAnalytics.UploadedBytes;
        }
    }
}
