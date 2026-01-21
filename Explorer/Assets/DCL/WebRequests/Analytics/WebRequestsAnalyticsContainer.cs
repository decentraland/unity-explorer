using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.GenericDelete;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        internal static readonly IWebRequestsAnalyticsContainer.RequestType[] SUPPORTED_REQUESTS =
        {
            new (typeof(GetAssetBundleWebRequest), "Asset Bundle"),
            new (typeof(GenericGetRequest), "Get"),
            new (typeof(PartialDownloadRequest), "Partial"),
            new (typeof(GenericPostRequest), "Post"),
            new (typeof(GenericPutRequest), "Put"),
            new (typeof(GenericPatchRequest), "Patch"),
            new (typeof(GenericHeadRequest), "Head"),
            new (typeof(GenericDeleteRequest), "Delete"),
            new (typeof(GetTextureWebRequest), "Texture"),
            new (typeof(GetAudioClipWebRequest), "Audio"),
        };

        private readonly Dictionary<Type, List<RequestMetricBase>> requestTypesWithMetrics = new ();
        private readonly Dictionary<Type, Func<RequestMetricBase>> requestMetricTypes = new ();

        private readonly List<RequestMetricBase> flatMetrics = new ();

        private readonly DebugWidgetBuilder? debugWidgetBuilder;
        private readonly SentryWebRequestHandler? sentryWebRequestHandler;

        private readonly Dictionary<UnityWebRequest, DateTime> pendingRequests = new (10);

        public IWebRequestsAnalyticsContainer.RequestType[] DebugRequestTypes { get; private set; } = Array.Empty<IWebRequestsAnalyticsContainer.RequestType>();

        public DebugWidgetVisibilityBinding? VisibilityBinding { get; private set; }

        public WebRequestsAnalyticsContainer(DebugWidgetBuilder? debugWidgetBuilder, SentryWebRequestHandler? sentryWebRequestHandler)
        {
            this.debugWidgetBuilder = debugWidgetBuilder;
            this.sentryWebRequestHandler = sentryWebRequestHandler;
        }

        public WebRequestsAnalyticsContainer BuildUpDebugWidget(bool isLocalSceneDevelopment)
        {
            DebugRequestTypes = isLocalSceneDevelopment
                ? Array.Empty<IWebRequestsAnalyticsContainer.RequestType>()
                : SUPPORTED_REQUESTS;

            debugWidgetBuilder?.SetVisibilityBinding(VisibilityBinding = new DebugWidgetVisibilityBinding(true));

            return this;
        }

        /// <summary>
        ///     Allows adding metrics dynamically without adding to the debug menu
        /// </summary>
        public WebRequestsAnalyticsContainer AddFlatMetric(RequestMetricBase metric)
        {
            flatMetrics.Add(metric);
            return this;
        }

        public IReadOnlyList<RequestMetricBase>? GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);

        public IDictionary<Type, Func<RequestMetricBase>> GetTrackedMetrics() =>
            requestMetricTypes;

        public WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: RequestMetricBase, new()
        {
            requestMetricTypes.Add(typeof(T), () => new T());
            return this;
        }

        public WebRequestsAnalyticsContainer Build()
        {
            foreach (IWebRequestsAnalyticsContainer.RequestType debugRequestType in DebugRequestTypes)
            {
                foreach ((Type? type, Func<RequestMetricBase>? ctor) in requestMetricTypes)
                {
                    RequestMetricBase? instance = ctor();

                    if (!requestTypesWithMetrics.TryGetValue(debugRequestType.Type, out List<RequestMetricBase> metrics))
                    {
                        metrics = new List<RequestMetricBase>();
                        requestTypesWithMetrics.Add(debugRequestType.Type, metrics);
                    }

                    instance.CreateDebugMenu(debugWidgetBuilder, debugRequestType);

                    metrics.Add(instance);
                }
            }

            return this;
        }

        public void RemoveFlatMetric(RequestMetricBase metric) =>
            flatMetrics.Remove(metric);

        void IWebRequestsAnalyticsContainer.OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request)
        {
            sentryWebRequestHandler?.OnRequestStarted(in envelope, request);

            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<RequestMetricBase> metrics))
                return;

            DateTime now = DateTime.Now;

            pendingRequests.Add(request.UnityWebRequest, now);

            foreach (RequestMetricBase? metric in metrics) { metric.OnRequestStarted(request, now); }

            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestStarted(request, now);
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request)
        {
            sentryWebRequestHandler?.OnRequestFinished(request);

            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<RequestMetricBase> metrics)) return;

            if (!pendingRequests.Remove(request.UnityWebRequest, out DateTime startTime))
                return;

            DateTime now = DateTime.Now;
            TimeSpan duration = now - startTime;

            foreach (RequestMetricBase? metric in metrics) { metric.OnRequestEnded(request, duration); }

            foreach (RequestMetricBase flat in flatMetrics)
                flat.OnRequestEnded(request, duration);
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataStarted<T>(T request)
        {
            sentryWebRequestHandler?.OnProcessDataStarted(request);
        }

        void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request)
        {
            sentryWebRequestHandler?.OnProcessDataFinished(request);
        }

        void IWebRequestsAnalyticsContainer.OnException<T>(T request, Exception exception)
        {
            sentryWebRequestHandler?.OnException(request, exception);
        }

        void IWebRequestsAnalyticsContainer.OnException<T>(T request, UnityWebRequestException exception)
        {
            sentryWebRequestHandler?.OnException(exception);
        }
    }
}
