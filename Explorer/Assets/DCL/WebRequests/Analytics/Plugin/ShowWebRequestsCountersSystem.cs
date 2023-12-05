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

        private readonly Dictionary<Type, ElementBinding<ulong>> ongoingRequests = new ();
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
                var binding = new ElementBinding<ulong>(0);
                ongoingRequests[requestType] = binding;

                widget.AddMarker(requestType.Name, binding, DebugLongMarkerDef.Unit.NoFormat);
            }
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsExpanded && lastTimeSinceMetricsUpdate > THROTTLE)
            {
                lastTimeSinceMetricsUpdate = 0;

                foreach (Type requestType in requestTypes)
                    ongoingRequests[requestType].Value = (ulong)webRequestsAnalyticsContainer.GetMetric(requestType);
            }

            lastTimeSinceMetricsUpdate += t;
        }
    }
}
