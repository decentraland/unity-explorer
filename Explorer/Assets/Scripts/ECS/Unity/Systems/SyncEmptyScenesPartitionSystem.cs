using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;

namespace ECS.Unity.Systems
{
    /// <summary>
    ///     Copies parent partition into `PartitionComponent` to avoid unnecessary recalculations
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.PRIORITIZATION)]
    public partial class SyncEmptyScenesPartitionSystem : BaseUnityLoopSystem
    {
        internal SyncEmptyScenesPartitionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SyncWithParentQuery(World);
        }

        [Query]
        private void SyncWithParent(ref PartitionComponent partitionComponent, ref IPartitionComponent parentPartition)
        {
            partitionComponent.Bucket = parentPartition.Bucket;
            partitionComponent.IsDirty = parentPartition.IsDirty;
            partitionComponent.RawSqrDistance = parentPartition.RawSqrDistance;
            partitionComponent.IsBehind = parentPartition.IsBehind;
        }
    }
}
