using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    /// <summary>
    ///     Reversed of <see cref="ServeTimePerMBAverage" />
    /// </summary>
    public class FillRateAverage : IRequestMetric
    {
        private readonly Dictionary<ITypedWebRequest, DateTime> pendingRequests = new (10);

        private ulong bytesTransferred;
        private double seconds;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.Bytes;

        public ulong GetMetric() =>
            (ulong)(bytesTransferred / seconds);

        public void OnRequestStarted(ITypedWebRequest request)
        {
            pendingRequests.Add(request, DateTime.Now);
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            if (!pendingRequests.Remove(request, out DateTime startTime))
                return;

            if (request.UnityWebRequest.downloadedBytes < ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            seconds += (DateTime.Now - startTime).TotalSeconds;
            bytesTransferred += request.UnityWebRequest.downloadedBytes;
        }
    }
}
