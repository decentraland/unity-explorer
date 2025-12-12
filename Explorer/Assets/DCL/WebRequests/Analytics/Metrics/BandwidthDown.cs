using DCL.DebugUtilities;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class BandwidthDown : RequestMetricBase
    {
        private ulong bandwidth { get; set; }

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.Bytes;

        public override ulong GetMetric() =>
            bandwidth;

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            if (request.UnityWebRequest.result == UnityWebRequest.Result.Success)
            {
                bandwidth += (request.UnityWebRequest.downloadedBytes);
            }
        }
    }
}
