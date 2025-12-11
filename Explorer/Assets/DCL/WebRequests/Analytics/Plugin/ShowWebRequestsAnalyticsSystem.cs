using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.Dumper;
using ECS.Abstract;
using System.Collections.Generic;
using Profiler = UnityEngine.Profiling.Profiler;
using static DCL.WebRequests.Analytics.IWebRequestsAnalyticsContainer;

namespace DCL.WebRequests.Analytics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ShowWebRequestsAnalyticsSystem : BaseUnityLoopSystem
    {
        private const float THROTTLE = 0.1f;

        private readonly IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer;
        private readonly RequestType[] requestTypes;

        private readonly DebugWidgetVisibilityBinding? visibilityBinding;

        private float lastTimeSinceMetricsUpdate;

        private ulong sumUpload;
        private ulong sumDownload;
        private ulong prevSumUpload;
        private ulong prevSumDownload;

        private bool metricsUpdatedThisFrame;

        internal ShowWebRequestsAnalyticsSystem(World world,
            IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer,
            DebugWidgetVisibilityBinding? visibilityBinding,
            RequestType[] requestTypes) : base(world)
        {
            this.webRequestsAnalyticsContainer = webRequestsAnalyticsContainer;
            this.requestTypes = requestTypes;
            this.visibilityBinding = visibilityBinding;
        }

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

        protected override void Update(float t)
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
                        IReadOnlyList<RequestMetricBase>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);
                        if (metrics == null) continue;

                        foreach (RequestMetricBase metric in metrics)
                            metric.UpdateDebugMenu();
                    }
                }
            }

            lastTimeSinceMetricsUpdate += t;
        }

        private void TryUpdateAllMetrics()
        {
            if (profilerEnabled || visibilityBinding is { IsExpanded: true } || WebRequestsDumper.Instance.Enabled)
            {
                if (metricsUpdatedThisFrame) return;

                foreach (RequestType requestType in requestTypes)
                {
                    IReadOnlyList<RequestMetricBase>? metrics = webRequestsAnalyticsContainer.GetMetric(requestType.Type);

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
    }
}
