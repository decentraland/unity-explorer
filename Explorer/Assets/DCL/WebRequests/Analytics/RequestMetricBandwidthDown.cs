using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics
{
    public class RequestMetricBandwidthDown : IRequestMetric
    {
        private ulong bandwidth { get; set; }

        public string Name => "RequestMetricBandwidthDown";

        public ulong GetMetric() =>
            bandwidth;

        public void OnRequestStarted(ITypedWebRequest request)
        {
            // Do nothing
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            if (request.UnityWebRequest.result == UnityWebRequest.Result.Success) { bandwidth += request.UnityWebRequest.downloadedBytes; }
        }
    }
}
