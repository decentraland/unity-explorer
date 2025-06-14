using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class TimeToFirstByteAverage : IRequestMetric
    {
        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public ulong GetMetric()
        {
            lock (this) { return (ulong)(sum / count) * 1_000_000UL; }
        }

        private void TrackFirstByteDownloaded(IWebRequest analytics)
        {
            lock (this)
            {
                count++;
                sum += (DateTime.Now - analytics.CreationTime).TotalMilliseconds;
            }
        }

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest)
        {
            webRequest.OnDownloadStarted += TrackFirstByteDownloaded;
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequest webRequest)
        {
            webRequest.OnDownloadStarted -= TrackFirstByteDownloaded;
        }
    }
}
