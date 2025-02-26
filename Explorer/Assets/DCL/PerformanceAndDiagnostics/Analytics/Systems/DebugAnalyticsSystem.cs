using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
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
        private readonly ElementBinding<ulong> binding;

        public DebugAnalyticsSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            var widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ANALYTICS);
            visibilityBinding = new DebugWidgetVisibilityBinding(true);
            binding = new ElementBinding<ulong>(0);

            if (widget == null)
            {
               return;
            }

            widget.SetVisibilityBinding(visibilityBinding);
            widget.AddMarker("Unflushed Count", binding, DebugLongMarkerDef.Unit.NoFormat);
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsConnectedAndExpanded == false)
                return;

            ulong value = RustSegmentAnalyticsService.UnflushedCount();
            binding.SetAndUpdate(value);
        }
    }
}
