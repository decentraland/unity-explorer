namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request) { }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request) { }
    }
}
