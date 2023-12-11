using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        public IReadOnlyList<IRequestMetric> GetMetric(Type requestType);

        internal void OnRequestStarted<T>(T request) where T: ITypedWebRequest;

        internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest;
    }
}
