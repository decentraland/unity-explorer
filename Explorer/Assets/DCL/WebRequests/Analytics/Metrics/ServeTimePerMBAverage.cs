using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class ServeTimePerMBAverage : IRequestMetric
    {
        // 10 KB, otherwise the error is too high
        internal const int SMALL_FILE_SIZE_FLOOR = 10 * 1024;

        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest)
        {
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequest webRequest)
        {
            double elapsedMs = (DateTime.Now - webRequest.CreationTime).TotalMilliseconds / BytesFormatter.Convert(webRequest.DownloadedBytes, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte);
            count++;
            sum += elapsedMs;
        }
    }
}
