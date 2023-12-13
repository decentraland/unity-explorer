using Arch.Core;
using Arch.Core.Utils;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Realm;
using System;
using Unity.Jobs;

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
        private PartitionJob partitionJob;
        private readonly JobScheduler.JobScheduler jobScheduler;

        private readonly QueryDescription PartitionQuery = new ()
        {
            All = new ComponentType[]
            {
                typeof(SceneDefinitionComponent),
                typeof(PartitionComponent),
            },
            Any = Array.Empty<ComponentType>(),
            None = Array.Empty<ComponentType>(),
            Exclusive = Array.Empty<ComponentType>(),
        };

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.partitionSettings = partitionSettings;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;
            partitionJob = new PartitionJob(this.partitionSettings, this.readOnlyCameraSamplingData);
        }

        protected override void Update(float t)
        {
            // Repartition if camera transform is qualified
            if (readOnlyCameraSamplingData.IsDirty)
            {
                World.InlineParallelQuery<PartitionJob, SceneDefinitionComponent, PartitionComponent>(in PartitionQuery, ref partitionJob);

                //PartitionExistingEntityQuery(World);
            }
            else
                ResetDirtyQuery(World);

            PartitionNewEntityQuery(World);
        }

        /*private void PartitionExistingEntity(in Entity entity)
        {
            var definition = World.Get<SceneDefinitionComponent>(entity);

            ScenesPartitioningUtils.Partition(partitionSettings, definition.ParcelsCorners, readOnlyCameraSamplingData, partitionComponent);
        }*/

        private readonly struct AnotherJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                throw new NotImplementedException();
            }
        }

        private readonly struct PartitionJob : IForEach<SceneDefinitionComponent, PartitionComponent>
        {
            private readonly IPartitionSettings settings;
            private readonly IReadOnlyCameraSamplingData onlyCameraSamplingData;

            public PartitionJob(IPartitionSettings partitionSettings, IReadOnlyCameraSamplingData cameraSamplingData)
            {
                settings = partitionSettings;
                onlyCameraSamplingData = cameraSamplingData;
            }

            public void Update(ref SceneDefinitionComponent t0, ref PartitionComponent t1)
            {
                ScenesPartitioningUtils.Partition(settings, t0.ParcelsCorners, onlyCameraSamplingData, t1);
            }
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
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            ScenesPartitioningUtils.Partition(partitionSettings, definition.ParcelsCorners, readOnlyCameraSamplingData, partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        private void PartitionExistingEntity(in Entity entity, ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            ScenesPartitioningUtils.Partition(partitionSettings, definition.ParcelsCorners, readOnlyCameraSamplingData, partitionComponent);
        }
    }
}
