using DCL.DebugUtilities;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    /// <summary>
    ///     Reversed of <see cref="ServeTimePerMBAverage" /> TODO Implemented improperly: fix me!
    /// </summary>
    internal class FillRateAverage : IRequestMetric
    {
        private ulong bytesTransferred;
        private double seconds;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.Bytes;

        public ulong GetMetric() =>
            (ulong)(bytesTransferred / seconds);

        void IRequestMetric.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest)
        {
        }

        void IRequestMetric.OnRequestEnded(ITypedWebRequest request, IWebRequest webRequest)
        {
            if (webRequest.DownloadedBytes < ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            seconds += (DateTime.Now - webRequest.CreationTime).TotalSeconds;
            bytesTransferred += webRequest.DownloadedBytes;
        }
    }
}
