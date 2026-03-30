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

        public override void OnRequestStarted<T>(T request, DateTime startTime)
        {
        }

        public override void OnRequestEnded<T>(T request, TimeSpan duration)
        {
            if (request.UnityWebRequest.result == UnityWebRequest.Result.Success)
            {
                bandwidth += (request.UnityWebRequest.downloadedBytes);
            }
        }
    }
}
