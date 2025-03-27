using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class ServerTimeSmallFileAverage : IRequestMetric
    {
        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequestAnalytics webRequestAnalytics, IWebRequest webRequest)
        {
            if (webRequestAnalytics.DownloadedBytes > ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            double elapsedMs = (DateTime.Now - webRequestAnalytics.CreationTime).TotalMilliseconds;
            count++;
            sum += elapsedMs;
        }
    }
}
