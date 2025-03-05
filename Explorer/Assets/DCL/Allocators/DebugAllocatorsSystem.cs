using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using Utility.Memory;

namespace DCL.Allocators
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.ALLOCATORS)]
    public partial class DebugAllocatorsSystem : BaseUnityLoopSystem
    {
        private readonly DebugWidgetVisibilityBinding visibilityBinding;
        private readonly ElementBinding<ulong> totalAllocatedMemory;
        private readonly ElementBinding<ulong> chunkSize;
        private readonly ElementBinding<ulong> chunksCount;
        private readonly ElementBinding<ulong> chunksInUseCount;

        public DebugAllocatorsSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            visibilityBinding = new DebugWidgetVisibilityBinding(true);

            totalAllocatedMemory = new ElementBinding<ulong>(0);
            chunkSize = new ElementBinding<ulong>(0);
            chunksCount = new ElementBinding<ulong>(0);
            chunksInUseCount = new ElementBinding<ulong>(0);

            var widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ANALYTICS);

            if (widget == null)
                return;

            widget.SetVisibilityBinding(visibilityBinding);
            widget.AddMarker("TotalAllocatedMemory", totalAllocatedMemory, DebugLongMarkerDef.Unit.Bytes);
            widget.AddMarker("ChunkSize", chunkSize, DebugLongMarkerDef.Unit.Bytes);
            widget.AddMarker("ChunksCount", chunksCount, DebugLongMarkerDef.Unit.NoFormat);
            widget.AddMarker("ChunksInUseCount", chunksInUseCount, DebugLongMarkerDef.Unit.NoFormat);
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsConnectedAndExpanded == false)
                return;

            var info = ISlabAllocator.SHARED.Info;

            totalAllocatedMemory.SetAndUpdate((ulong)info.TotalAllocatedMemory);
            chunkSize.SetAndUpdate((ulong)info.ChunkSize);
            chunksCount.SetAndUpdate((ulong)info.ChunksCount);
            chunksInUseCount.SetAndUpdate((ulong)info.ChunksInUseCount);
        }
    }
}
