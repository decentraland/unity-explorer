using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace DCL.ECS.GlobalPartitioning
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ProcessBatchesSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription BATCH_IN_PROGRESS = new QueryDescription().WithAll<BatchInProgress>();

        public ProcessBatchesSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            // If there is a batch in progress do nothing
            if (World.CountEntities(BATCH_IN_PROGRESS) > 0) return;

            // First re-evaluate existing batches to update their priority and possibly to unload them if required
        }
    }
}
