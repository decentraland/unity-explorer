using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.WebRequests.Analytics.Metrics
{
    internal class TimeToFirstByteAverage : IRequestMetric
    {
        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        private void TrackFirstByteDownloaded(IWebRequest analytics)
        {
            count++;
            sum += (DateTime.Now - analytics.CreationTime).TotalMilliseconds;
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
