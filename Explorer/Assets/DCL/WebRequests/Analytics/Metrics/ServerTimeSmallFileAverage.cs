using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class ServerTimeSmallFileAverage : IRequestMetric
    {
        private readonly Dictionary<ITypedWebRequest, DateTime> pendingRequests = new (10);

        private double sum;
        private uint count;

        public DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.TimeNanoseconds;

        public ulong GetMetric() =>
            (ulong)(sum / count) * 1_000_000UL;

        public void OnRequestStarted(ITypedWebRequest request)
        {
            pendingRequests.Add(request, DateTime.Now);
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            if (!pendingRequests.Remove(request, out DateTime startTime))
                return;

            if (request.UnityWebRequest.downloadedBytes > ServeTimePerMBAverage.SMALL_FILE_SIZE_FLOOR) return;

            double elapsedMs = (DateTime.Now - startTime).TotalMilliseconds;
            count++;
            sum += elapsedMs;
        }
    }
}
