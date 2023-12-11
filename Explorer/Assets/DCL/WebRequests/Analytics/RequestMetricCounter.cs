using System;

namespace DCL.WebRequests.Analytics
{
    public class RequestMetricCounter : IRequestMetric
    {
        public RequestMetricCounter() { }

        private int counter { get; set; }

        public string Name => "Active Request Counter";

        public int GetMetric() =>
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
