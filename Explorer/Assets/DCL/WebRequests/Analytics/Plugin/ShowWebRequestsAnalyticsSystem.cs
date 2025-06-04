using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using DCL.WebRequests.Analytics.Metrics;
using ECS.Abstract;
using System;
using System.Collections.Generic;

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

        internal ShowWebRequestsAnalyticsSystem(World world,
            IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            RequestType[] requestTypes) : base(world)
        {
            this.webRequestsAnalyticsContainer = webRequestsAnalyticsContainer;
            this.requestTypes = requestTypes;

            DebugWidgetBuilder? widget = debugContainerBuilder
                                       .TryAddWidget("Web Requests")
                                      ?.SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true));

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

        ulong prevSumUpload = 0;
        ulong prevSumDownload = 0;

        protected override void Update(float t)
        {
#if ENABLE_PROFILER
            ulong sumUpload = 0;
            ulong sumDownload = 0;

            foreach (RequestType requestType in requestTypes)
            {
                IReadOnlyList<IRequestMetric>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);
                if (metrics == null) continue;

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

            NetworkProfilerCounters.WEB_REQUESTS_UPLOADED.Value = sumUpload;
            NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED.Value = sumDownload;
            NetworkProfilerCounters.WEB_REQUESTS_UPLOADED_FRAME.Value = sumUpload - prevSumUpload;
            NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED_FRAME.Value = sumDownload - prevSumDownload;

            prevSumUpload = sumUpload;
            prevSumDownload = sumDownload;
#endif

            if (visibilityBinding is { IsExpanded: true })
            {
                // Some metrics may require update without throttling
                foreach (RequestType requestType in requestTypes)
                {
                    IReadOnlyList<IRequestMetric>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);
                    if (metrics == null) continue;

                    foreach (IRequestMetric metric in metrics)
                        metric.Update();
                }

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
    }
}
