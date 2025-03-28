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

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest) { }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequest webRequest)
        {
            if (webRequest.Response.IsSuccess) counter++;
        }
    }
}
