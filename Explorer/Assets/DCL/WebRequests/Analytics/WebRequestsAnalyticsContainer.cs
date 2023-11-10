using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<Type, int> ongoingRequests = new ();

        public int GetMetric(Type requestType) =>
            ongoingRequests.TryGetValue(requestType, out int count) ? count : 0;

        void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request)
        {
            if (ongoingRequests.TryGetValue(typeof(T), out int count))
                ongoingRequests[typeof(T)] = count + 1;
            else
                ongoingRequests.Add(typeof(T), 1);
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (ongoingRequests.TryGetValue(typeof(T), out int count))
                ongoingRequests[typeof(T)] = count - 1;
        }
    }
}
