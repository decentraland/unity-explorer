using DCL.DebugUtilities;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class TotalFailed : RequestMetricBase
    {
        private ulong counter { get; set; }

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.NoFormat;

        public override ulong GetMetric() =>
            counter;

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
       }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            if (request.UnityWebRequest.result != UnityWebRequest.Result.Success) counter++;
        }
    }
}
