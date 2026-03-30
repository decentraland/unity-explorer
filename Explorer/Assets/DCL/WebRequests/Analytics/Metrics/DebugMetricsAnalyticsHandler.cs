using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using Profiler = UnityEngine.Profiling.Profiler;
using static DCL.WebRequests.Analytics.IWebRequestsAnalyticsContainer;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class DebugMetricsAnalyticsHandler : IWebRequestAnalyticsHandler
    {
        internal static readonly RequestType[] SUPPORTED_REQUESTS =
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

        private const float THROTTLE = 0.1f;

        private DebugWidgetVisibilityBinding? visibilityBinding;
        private RequestType[] requestTypes;
        private readonly DebugWidgetBuilder? debugWidgetBuilder;

        private readonly Dictionary<Type, List<RequestMetricBase>> requestTypesWithMetrics = new ();
        private readonly Dictionary<Type, Func<RequestMetricBase>> requestMetricTypes = new ();

        private float lastTimeSinceMetricsUpdate;

        private ulong sumUpload;
        private ulong sumDownload;
        private ulong prevSumUpload;
        private ulong prevSumDownload;

        private bool metricsUpdatedThisFrame;

        private static bool profilerEnabled
        {
            get
            {
#if ENABLE_PROFILER
                return Profiler.enabled && Profiler.IsCategoryEnabled(NetworkProfilerCounters.CATEGORY);
#endif
                return false;
            }
        }

        public DebugMetricsAnalyticsHandler(DebugWidgetBuilder? debugWidgetBuilder)
        {
            this.debugWidgetBuilder = debugWidgetBuilder;
            requestTypes = Array.Empty<RequestType>();
        }

        public DebugMetricsAnalyticsHandler BuildUpDebugWidget(bool isLocalSceneDevelopment)
        {
            var visibilityBinding = new DebugWidgetVisibilityBinding(true);

            RequestType[] debugRequestTypes = isLocalSceneDevelopment
                ? Array.Empty<RequestType>()
                : SUPPORTED_REQUESTS;

            debugWidgetBuilder.SetVisibilityBinding(visibilityBinding);

            this.visibilityBinding = visibilityBinding;
            requestTypes = debugRequestTypes;

            return this;
        }

        public DebugMetricsAnalyticsHandler AddTrackedMetric<T>() where T: RequestMetricBase, new()
        {
            requestMetricTypes.Add(typeof(T), () => new T());
            return this;
        }

        public DebugMetricsAnalyticsHandler Build()
        {
            foreach (RequestType debugRequestType in requestTypes)
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

        public void Update(float dt)
        {
            metricsUpdatedThisFrame = false;

            sumUpload = 0;
            sumDownload = 0;

            TryUpdateAllMetrics();

#if ENABLE_PROFILER
            if (profilerEnabled)
            {
                NetworkProfilerCounters.WEB_REQUESTS_UPLOADED.Value = sumUpload;
                NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED.Value = sumDownload;
                NetworkProfilerCounters.WEB_REQUESTS_UPLOADED_FRAME.Value = sumUpload - prevSumUpload;
                NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED_FRAME.Value = sumDownload - prevSumDownload;

                prevSumUpload = sumUpload;
                prevSumDownload = sumDownload;
            }
#endif

            if (visibilityBinding is { IsExpanded: true })
            {
                if (lastTimeSinceMetricsUpdate > THROTTLE)
                {
                    lastTimeSinceMetricsUpdate = 0;

                    foreach (RequestType requestType in requestTypes)
                    {
                        IReadOnlyList<RequestMetricBase>? metrics = GetMetric(requestType.Type);
                        if (metrics == null) continue;

                        foreach (RequestMetricBase metric in metrics)
                            metric.UpdateDebugMenu();
                    }
                }
            }

            lastTimeSinceMetricsUpdate += dt;
        }

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct { }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<RequestMetricBase> metrics))
                return;

            foreach (RequestMetricBase? metric in metrics)
                metric.OnRequestStarted(request, startedAt);
        }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            if (!requestTypesWithMetrics.TryGetValue(typeof(T), out List<RequestMetricBase> metrics)) return;

            foreach (RequestMetricBase? metric in metrics)
                metric.OnRequestEnded(request, duration);
        }

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest { }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            OnRequestFinished(request, duration);
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest
        {
            OnRequestFinished(request, duration);
        }

        private void TryUpdateAllMetrics()
        {
            if (profilerEnabled || visibilityBinding is { IsExpanded: true })
            {
                if (metricsUpdatedThisFrame) return;

                foreach (RequestType requestType in requestTypes)
                {
                    IReadOnlyList<RequestMetricBase>? metrics = GetMetric(requestType.Type);

                    if (metrics == null)
                        continue;

                    foreach (RequestMetricBase metric in metrics)
                    {
                        metric.Update();

                        switch (metric)
                        {
                            case BandwidthUp: sumUpload += metric.GetMetric(); break;
                            case BandwidthDown: sumDownload += metric.GetMetric(); break;
                        }
                    }
                }

                metricsUpdatedThisFrame = true;
            }
        }

        public IDictionary<Type, Func<RequestMetricBase>> GetTrackedMetrics() =>
            requestMetricTypes;

        private IReadOnlyList<RequestMetricBase>? GetMetric(Type requestType) =>
            requestTypesWithMetrics.GetValueOrDefault(requestType);
    }
}
