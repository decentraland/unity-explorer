namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        public ulong GetMetric();
        public void OnRequestStarted(ITypedWebRequest request);
        public void OnRequestEnded(ITypedWebRequest request);
    }
}
