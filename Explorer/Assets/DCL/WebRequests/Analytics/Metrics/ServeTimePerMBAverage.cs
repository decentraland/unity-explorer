using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class ServeTimePerMBAverage : RequestMetricBase
    {
        // 10 KB, otherwise the error is too high
        internal const int SMALL_FILE_SIZE_FLOOR = 10 * 1024;

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
            if (request.UnityWebRequest.downloadedBytes < SMALL_FILE_SIZE_FLOOR) return;

            double elapsedMs = duration.TotalMilliseconds / BytesFormatter.Convert(request.UnityWebRequest.downloadedBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte);
            count++;
            sum += elapsedMs;
        }
    }
}
