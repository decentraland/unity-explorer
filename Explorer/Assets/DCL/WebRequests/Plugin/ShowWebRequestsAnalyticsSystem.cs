using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ShowWebRequestsAnalyticsSystem : BaseUnityLoopSystem
    {
        private const float THROTTLE = 0.1f;

        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly DebugWidgetVisibilityBinding? visibilityBinding;

        private readonly IReadOnlyList<MetricRegistration> metrics;

        private float lastTimeSinceMetricsUpdate;

        internal ShowWebRequestsAnalyticsSystem(World world, IWebRequestsAnalyticsContainer analyticsContainer, DebugWidgetBuilder? widget) : base(world)
        {
            this.analyticsContainer = analyticsContainer;
            widget?.SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(false));

            metrics = analyticsContainer.GetTrackedMetrics();
        }

        protected override void Update(float t)
        {
            if (visibilityBinding is { IsExpanded: true })
            {
                if (lastTimeSinceMetricsUpdate > THROTTLE)
                {
                    lastTimeSinceMetricsUpdate = 0;

                    foreach (MetricRegistration metricRegistration in metrics)
                        metricRegistration.UpdateBinding();

                    foreach (MetricRegistration.AggregatedMetric aggregatedMetric in analyticsContainer.GetAggregatedMetrics())
                        aggregatedMetric.UpdateBinding();
                }
            }

            lastTimeSinceMetricsUpdate += t;
        }
    }
}
