using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics();

        IReadOnlyList<IRequestMetric> GetMetric(Type requestType);

        internal void OnRequestStarted<T>(T request) where T: ITypedWebRequest;

        internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest;

        public static readonly IWebRequestsAnalyticsContainer DEFAULT = new WebRequestsAnalyticsContainer();
    }

    public interface IMutableWebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: class, IRequestMetric, new();
    }

    public static class WebRequestsAnalyticsExtensions
    {
        internal static async UniTask WithAnalyticsAsync<T>(this T request, IWebRequestsAnalyticsContainer analyticsContainer, UniTask innerTask) where T: ITypedWebRequest
        {
            try
            {
                analyticsContainer.OnRequestStarted(request);
                await innerTask;
            }
            finally
            {
                // Regardless of the exception at this moment request is not disposed of, and, thus, can be read from
                analyticsContainer.OnRequestFinished(request);
            }
        }
    }
}
