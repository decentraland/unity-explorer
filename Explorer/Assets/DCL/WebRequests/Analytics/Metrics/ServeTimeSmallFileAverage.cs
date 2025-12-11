using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class ServeTimeSmallFileAverage : RequestMetricBase
    {
        private double sum;
        private uint count;

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public override ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            if (request.UnityWebRequest.downloadedBytes > ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            double elapsedMs = duration.TotalMilliseconds;
            count++;
            sum += elapsedMs;
        }
    }
}
