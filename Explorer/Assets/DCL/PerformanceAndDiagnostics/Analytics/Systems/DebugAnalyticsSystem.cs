using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.Abstract;
using Plugins.RustSegment.SegmentServerWrap;

namespace DCL.Analytics.Systems
{
    /// <summary>
    /// Not supposed to work in Editor.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DebugAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        public DebugAnalyticsSystem(World world, IAnalyticsController analyticsController, IDebugContainerBuilder debugBuilder) : base(world)
        {
            visibilityBinding = new DebugWidgetVisibilityBinding(true);
            var widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ANALYTICS);

            if (widget == null)
                return;

            var serviceBinding = new ElementBinding<string>(analyticsController.ServiceInfo);

            widget.SetVisibilityBinding(visibilityBinding);
            widget.AddCustomMarker("Service", serviceBinding);
            widget.AddSingleButton("Manual Flush", analyticsController.Flush);
        }

        protected override void Update(float t) { }
    }
}
