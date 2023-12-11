namespace DCL.WebRequests.Analytics
{
    public interface IRequestMetric
    {
        public string Name { get; }
        public ulong GetMetric();
        public void OnRequestStarted(ITypedWebRequest request);
        public void OnRequestEnded(ITypedWebRequest request);
    }
}
