using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    /// <summary>
    ///     Reversed of <see cref="ServeTimePerMBAverage" />
    /// </summary>
    public class FillRateAverage : RequestMetricBase
    {
        private ulong bytesTransferred;
        private double seconds;

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.Bytes;

        public override ulong GetMetric() =>
            (ulong)(bytesTransferred / seconds);

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            if (request.UnityWebRequest.downloadedBytes < ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            seconds += duration.TotalSeconds;
            bytesTransferred += request.UnityWebRequest.downloadedBytes;
        }
    }
}
