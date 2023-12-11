using System;

namespace DCL.WebRequests.Analytics
{
    public class RequestMetricCounter : IRequestMetric
    {
        private ulong counter { get; set; }

        public string Name => "RequestMetricCounter";

        public ulong GetMetric() =>
            counter;

        public void OnRequestStarted(ITypedWebRequest request)
        {
            counter++;
        }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            counter--;
        }
    }
}
