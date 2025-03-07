using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Optimization.Memory;
using ECS.Abstract;

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
        private readonly ElementBinding<ulong> totalReturnedTimes;
        private readonly ElementBinding<ulong> totalAllocatedTimes;

        public DebugAllocatorsSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            visibilityBinding = new DebugWidgetVisibilityBinding(true);

            totalAllocatedMemory = new ElementBinding<ulong>(0);
            chunkSize = new ElementBinding<ulong>(0);
            chunksCount = new ElementBinding<ulong>(0);
            chunksInUseCount = new ElementBinding<ulong>(0);
            totalReturnedTimes = new ElementBinding<ulong>(0);
            totalAllocatedTimes = new ElementBinding<ulong>(0);

            var widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ALLOCATORS);

            if (widget == null)
                return;

            widget.SetVisibilityBinding(visibilityBinding);
            widget.AddMarker("TotalAllocatedMemory", totalAllocatedMemory, DebugLongMarkerDef.Unit.Bytes);
            widget.AddMarker("ChunkSize", chunkSize, DebugLongMarkerDef.Unit.Bytes);
            widget.AddMarker("ChunksCount", chunksCount, DebugLongMarkerDef.Unit.NoFormat);
            widget.AddMarker("ChunksInUseCount", chunksInUseCount, DebugLongMarkerDef.Unit.NoFormat);
            widget.AddMarker("TotalReturnedTimes", totalReturnedTimes, DebugLongMarkerDef.Unit.NoFormat);
            widget.AddMarker("TotalAllocatedTimes", totalAllocatedTimes, DebugLongMarkerDef.Unit.NoFormat);
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsConnectedAndExpanded == false)
                return;

            var info = ISlabAllocator.SHARED.Info;

            totalAllocatedMemory.SetAndUpdate(info.TotalAllocatedMemory);
            chunkSize.SetAndUpdate((ulong)info.ChunkSize);
            chunksCount.SetAndUpdate((ulong)info.ChunksCount);
            chunksInUseCount.SetAndUpdate((ulong)info.ChunksInUseCount);
            totalReturnedTimes.SetAndUpdate(info.ReturnedTimes);
            totalAllocatedTimes.SetAndUpdate(info.AllocatedTimes);
        }
    }
}
