using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class ServeTimeSmallFileAverage : IRequestMetric
    {
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
            if (webRequest.DownloadedBytes is > 0 and > ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            double elapsedMs = (DateTime.Now - webRequest.CreationTime).TotalMilliseconds;

            lock (this)
            {
                count++;
                sum += elapsedMs;
            }
        }
    }
}
