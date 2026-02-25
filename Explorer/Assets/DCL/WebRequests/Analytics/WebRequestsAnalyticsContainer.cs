using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private readonly Dictionary<UnityWebRequest, DateTime> pendingRequests = new (10);
        private readonly List<IWebRequestAnalyticsHandler> handlers;

        public WebRequestsAnalyticsContainer(params IWebRequestAnalyticsHandler?[] handlers)
        {
            this.handlers = new List<IWebRequestAnalyticsHandler>(handlers.Where(h => h != null)!);
        }

        void IWebRequestsAnalyticsContainer.OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request)
        {
            pendingRequests.Add(request.UnityWebRequest, DateTime.MinValue);

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnBeforeBudgeting(in envelope, request);
        }

        void IWebRequestsAnalyticsContainer.OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request)
        {
            DateTime now = DateTime.Now;

            if (!pendingRequests.ContainsKey(request.UnityWebRequest)) return;

            pendingRequests[request.UnityWebRequest] = now;

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnRequestStarted(in envelope, request, now);
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            if (!pendingRequests.TryGetValue(request.UnityWebRequest, out DateTime startTime))
                return;

            DateTime now = DateTime.Now;
            TimeSpan duration = now - startTime;

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnRequestFinished(request, duration);
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request)
        {
            if (!pendingRequests.Remove(request.UnityWebRequest))
                return;

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnProcessDataFinished(request);
        }

        void IWebRequestsAnalyticsContainer.OnException<T>(T request, Exception exception)
        {
            if (!pendingRequests.Remove(request.UnityWebRequest, out DateTime startTime))
                return;

            DateTime now = DateTime.Now;
            TimeSpan duration = now - startTime;

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnException(request, exception, duration);
        }

        void IWebRequestsAnalyticsContainer.OnException<T>(T request, UnityWebRequestException exception)
        {
            if (!pendingRequests.Remove(request.UnityWebRequest, out DateTime startTime))
                return;

            DateTime now = DateTime.Now;
            TimeSpan duration = now - startTime;

            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.OnException(request, exception, duration);
        }

        void IWebRequestsAnalyticsContainer.Update(float dt)
        {
            foreach (IWebRequestAnalyticsHandler? handler in handlers)
                handler.Update(dt);
        }
    }
}
