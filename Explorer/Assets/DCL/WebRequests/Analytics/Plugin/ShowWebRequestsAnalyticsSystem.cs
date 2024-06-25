using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ShowWebRequestsAnalyticsSystem : BaseUnityLoopSystem
    {
        private const float THROTTLE = 0.1f;

        private readonly IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer;
        private readonly Type[] requestTypes;

        private readonly Dictionary<Type, Dictionary<string, ElementBinding<ulong>>> ongoingRequests = new ();
        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        private float lastTimeSinceMetricsUpdate;

        internal ShowWebRequestsAnalyticsSystem(World world,
            IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Type[] requestTypes) : base(world)
        {
            this.webRequestsAnalyticsContainer = webRequestsAnalyticsContainer;
            this.requestTypes = requestTypes;

            DebugWidgetBuilder widget = debugContainerBuilder
                                       .AddWidget("Web Requests")
                                       .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true));

            foreach (Type requestType in requestTypes)
            {
                var bindings = new Dictionary<string, ElementBinding<ulong>>(0);
                var metrics = webRequestsAnalyticsContainer.GetTrackedMetrics();

                foreach (var metric in metrics)
                {
                    bindings.Add(metric.Key.Name, new ElementBinding<ulong>(0));
                    var requestMetricUnit = metric.Value().GetUnit();
                    widget.AddMarker(requestType.Name + "-" + metric.Key.Name, bindings[metric.Key.Name], requestMetricUnit);
                }

                ongoingRequests[requestType] = bindings;
            }
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsExpanded && lastTimeSinceMetricsUpdate > THROTTLE)
            {
                lastTimeSinceMetricsUpdate = 0;

                foreach (Type requestType in requestTypes)
                {
                    var metrics = webRequestsAnalyticsContainer.GetMetric(requestType);
                    if (metrics == null) continue;

                    foreach (var metric in metrics)
                    {
                        if (ongoingRequests.TryGetValue(requestType, out Dictionary<string, ElementBinding<ulong>> bindings) &&
                            bindings.TryGetValue(metric.GetType().Name, out ElementBinding<ulong> binding)) { binding.Value = metric.GetMetric(); }
                    }
                }
            }

            lastTimeSinceMetricsUpdate += t;
        }
    }
}
