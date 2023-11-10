using System;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        public int GetMetric(Type requestType);

        internal void OnRequestStarted<T>(T request) where T: ITypedWebRequest;

        internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest;
    }
}
