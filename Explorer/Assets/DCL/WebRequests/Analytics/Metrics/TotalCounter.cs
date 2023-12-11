namespace DCL.WebRequests.Analytics.Metrics
{
    public class TotalCounter : IRequestMetric
    {
        private ulong counter { get; set; }

        public ulong GetMetric() =>
            counter;

        public void OnRequestStarted(ITypedWebRequest request)
        {
       }

        public void OnRequestEnded(ITypedWebRequest request)
        {
            counter++;
        }
    }
}
