using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CDPBridges;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.ChromeDevtool;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;

namespace DCL.WebRequests.Analytics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ShowWebRequestsAnalyticsSystem : BaseUnityLoopSystem
    {
        public readonly struct RequestType
        {
            public readonly Type Type;
            public readonly string MarkerName;

            public RequestType(Type type, string markerName)
            {
                Type = type;
                MarkerName = markerName;
            }
        }

        private const float THROTTLE = 0.1f;

        private readonly IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer;
        private readonly RequestType[] requestTypes;

        private readonly Dictionary<Type, Dictionary<string, ElementBinding<ulong>>> ongoingRequests = new ();
        private readonly DebugWidgetVisibilityBinding? visibilityBinding;

        private float lastTimeSinceMetricsUpdate;

        private ulong sumUpload;
        private ulong sumDownload;
        private ulong prevSumUpload;
        private ulong prevSumDownload;

        private bool metricsUpdatedThisFrame;

        internal ShowWebRequestsAnalyticsSystem(World world,
            IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient,
            RequestType[] requestTypes) : base(world)
        {
            this.webRequestsAnalyticsContainer = webRequestsAnalyticsContainer;
            this.requestTypes = requestTypes;

            DebugWidgetBuilder? widget = debugContainerBuilder
                                        .TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS)
                                       ?.AddSingleButton("Open Chrome DevTools", () =>
                                         {
                                             BridgeStartResult result = chromeDevtoolProtocolClient.StartAndOpen();
                                             string? errorMessage = ErrorMessageFromBridgeResult(result);

                                             if (errorMessage != null)
                                                 NotificationsBusController
                                                    .Instance
                                                    .AddNotification(new ServerErrorNotification(errorMessage));
                                         })
                                        .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true));

            foreach (RequestType requestType in requestTypes)
            {
                var bindings = new Dictionary<string, ElementBinding<ulong>>(0);
                var metrics = webRequestsAnalyticsContainer.GetTrackedMetrics();

                foreach (var metric in metrics)
                {
                    bindings.Add(metric.Key.Name, new ElementBinding<ulong>(0));
                    DebugLongMarkerDef.Unit requestMetricUnit = metric.Value().GetUnit();
                    widget?.AddMarker(requestType.MarkerName + "-" + metric.Key.Name, bindings[metric.Key.Name], requestMetricUnit);
                }

                ongoingRequests[requestType.Type] = bindings;
            }
        }

        // ReSharper disable once ReturnTypeCanBeNotNullable
        private static string? ErrorMessageFromBridgeResult(BridgeStartResult result)
        {
            string message = result.Match(
                onSuccess: static () => null!,
                onBridgeStartError: static e => e.Match(
                    onWebSocketError: static e => $"Cannot start WebSocket server: {e.Exception.Message}",
                    onBrowserOpenError: static e => e.Match(
                        onErrorChromeNotInstalled: static () => "Chrome not installed",
                        onException: static e => $"Cannot open DevTools: {e.Message}")
                )
            );

            return message;
        }

        protected override void Update(float t)
        {
            metricsUpdatedThisFrame = false;
#if ENABLE_PROFILER
            if (UnityEngine.Profiling.Profiler.enabled && UnityEngine.Profiling.Profiler.IsCategoryEnabled(NetworkProfilerCounters.CATEGORY))
            {
                sumUpload = 0;
                sumDownload = 0;

                UpdateAllMetrics();

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
                // Some metrics may require update without throttling
                UpdateAllMetrics();

                if (lastTimeSinceMetricsUpdate > THROTTLE)
                {
                    lastTimeSinceMetricsUpdate = 0;

                    foreach (RequestType requestType in requestTypes)
                    {
                        IReadOnlyList<IRequestMetric>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);
                        if (metrics == null) continue;

                        foreach (IRequestMetric? metric in metrics)
                        {
                            if (ongoingRequests.TryGetValue(requestType.Type, out Dictionary<string, ElementBinding<ulong>> bindings) &&
                                bindings.TryGetValue(metric.GetType().Name, out ElementBinding<ulong> binding)) { binding.Value = metric.GetMetric(); }
                        }
                    }
                }
            }

            lastTimeSinceMetricsUpdate += t;
        }

        private void UpdateAllMetrics()
        {
            if (metricsUpdatedThisFrame) return;

            foreach (RequestType requestType in requestTypes)
            {
                IReadOnlyList<IRequestMetric>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);

                if (metrics == null)
                    continue;

                foreach (IRequestMetric metric in metrics)
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
}
