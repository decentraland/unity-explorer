using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics();

        IReadOnlyList<IRequestMetric>? GetMetric(Type requestType);

        internal void OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest);

        internal void OnRequestFinished(ITypedWebRequest request, IWebRequest webRequest);


        public static readonly IWebRequestsAnalyticsContainer DEFAULT = new WebRequestsAnalyticsContainer();
    }
}
