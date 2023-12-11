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
    public partial class ShowWebRequestsCountersSystem : BaseUnityLoopSystem
    {
        private const float THROTTLE = 0.1f;

        private readonly IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer;
        private readonly Type[] requestTypes;

        private readonly Dictionary<Type, Dictionary<string, ElementBinding<ulong>>> ongoingRequests = new ();
        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        private float lastTimeSinceMetricsUpdate;

        internal ShowWebRequestsCountersSystem(World world,
            IWebRequestsAnalyticsContainer webRequestsAnalyticsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Type[] requestTypes) : base(world)
        {
            this.webRequestsAnalyticsContainer = webRequestsAnalyticsContainer;
            this.requestTypes = requestTypes;

            DebugWidgetBuilder widget = debugContainerBuilder.AddWidget("Web Requests")
                                                             .SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true));

            foreach (Type requestType in requestTypes)
            {
                var bindings = new Dictionary<string, ElementBinding<ulong>>(0);
                var metrics = webRequestsAnalyticsContainer.GetMetric(requestType);
                foreach (IRequestMetric metric in metrics)
                {
                    widget.AddMarker(requestType.Name + metric.Name, bindings[metric.Name], DebugLongMarkerDef.Unit.NoFormat);
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
                    foreach (var metric in metrics)
                    {
                        ongoingRequests[requestType][metric.Name].Value = (ulong)metric.GetMetric();
                    }
                }
            }

            lastTimeSinceMetricsUpdate += t;
        }
    }
}
