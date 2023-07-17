using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Realm;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Partitions scene entities right after their definitions are resolved so their loading is properly deferred
    ///     according to the assigned partition. Partitioning performed for non-empty scenes only
    ///     <para>
    ///         Partitioning is performed according to the closest scene parcel to the camera.
    ///         It is guaranteed that parcels array is set in a scene definition, otherwise it won't work
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadPointersByRadiusSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateBefore(typeof(ResolveStaticPointersSystem))]
    public partial class PartitionSceneEntitiesSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData readOnlyCameraSamplingData;

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.partitionSettings = partitionSettings;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;
        }

        protected override void Update(float t)
        {
            // Repartition if camera transform is qualified
            if (readOnlyCameraSamplingData.IsDirty)
                PartitionExistingEntityQuery(World);
            else
                ResetDirtyQuery(World);

            PartitionNewEntityQuery(World);
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        private void ResetDirty(ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntity(in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (definition.IsEmpty) return;

            PartitionComponent partitionComponent = partitionComponentPool.Get();
            ScenesPartitioningUtils.Partition(partitionSettings, definition.ParcelsCorners, readOnlyCameraSamplingData, partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        private void PartitionExistingEntity(ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            ScenesPartitioningUtils.Partition(partitionSettings, definition.ParcelsCorners, readOnlyCameraSamplingData, partitionComponent);
        }
    }
}
