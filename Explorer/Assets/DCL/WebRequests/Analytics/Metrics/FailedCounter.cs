using DCL.DebugUtilities;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class TotalFailed : IRequestMetric
    {
        private ulong counter { get; set; }

        public DebugLongMarkerDef.Unit GetUnit() => DebugLongMarkerDef.Unit.NoFormat;

        public ulong GetMetric() =>
            counter;

        public void OnRequestStarted(ITypedWebRequest request)
        {
       }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            if (request.UnityWebRequest.result != UnityWebRequest.Result.Success) counter++;
        }
    }
}
