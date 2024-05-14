using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Unity.Collections;
using Unity.Jobs;
using static ECS.Prioritization.ScenesPartitioningUtils;
using static Utility.ParcelMathHelper;

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
    [UpdateBefore(typeof(ResolveStaticPointersSystem))]
    public partial class PartitionSceneEntitiesSystem : BaseUnityLoopSystem
    {
        private const int DEPLOYED_SCENES_LIMIT = 90000; // 300x300 scenes (without empty)

        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly IReadOnlyCameraSamplingData readOnlyCameraSamplingData;
        private readonly JobScheduler.JobScheduler jobScheduler;

        private readonly byte emptyScenePartition;


        protected PartitionDataContainer partitionDataContainer;

        private JobHandle partitionJobHandle;
        private bool isRunningJob;

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData,
            PartitionDataContainer partitionDataContainer) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;
            this.partitionDataContainer = partitionDataContainer;


            partitionDataContainer.Initialize(DEPLOYED_SCENES_LIMIT, partitionSettings.SqrDistanceBuckets, partitionSettings);
            emptyScenePartition = (byte)(partitionSettings.SqrDistanceBuckets.Count - 1);
        }

        public override void Dispose()
        {
            partitionJobHandle.Complete();
            partitionDataContainer.Dispose();


        }

        internal void ForceCompleteJob()
        {
            partitionJobHandle.Complete();
        }

        protected override void Update(float t)
        {
            // once the job is completed, we query and update all partitions
            if (isRunningJob && partitionJobHandle.IsCompleted)
            {
                partitionJobHandle.Complete();
                isRunningJob = false;
                PartitionExistingEntityQuery(World);
            }

            if (!isRunningJob)
            {
                PartitionNewEntityQuery(World);
            }

            // Repartition if camera transform is qualified and the last job has already been completed
            if (readOnlyCameraSamplingData.IsDirty && !isRunningJob && partitionDataContainer.currentPartitionIndex > 0)
            {
                partitionJobHandle = partitionDataContainer.ScheduleJob(readOnlyCameraSamplingData);
                isRunningJob = true;
            }
        }

        [Query]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntity(in Entity entity, ref SceneDefinitionComponent definition)
        {
            // If we partition empty scene then their number can grow infinitely as we don't have boundaries
            if (definition.IsEmpty)
            {
                PartitionComponent partitionComponent = partitionComponentPool.Get();
                // some default values to not break other systems
                partitionComponent.Bucket = emptyScenePartition;
                World.Add(entity, partitionComponent);
                return;
            }

            if (definition.InternalJobIndex < 0)
            {
                ScheduleSceneDefinition(ref definition);
            }
            else
            {
                var partition = partitionDataContainer.partitions[definition.InternalJobIndex];
                PartitionComponent partitionComponent = partitionComponentPool.Get();
                partitionComponent.IsDirty = partition.IsDirty;
                partitionComponent.IsBehind = partition.IsBehind;
                partitionComponent.Bucket = partition.Bucket;
                partitionComponent.RawSqrDistance = partition.RawSqrDistance;
                World.Add(entity, partitionComponent);
            }
        }

        protected void ScheduleSceneDefinition(ref SceneDefinitionComponent definition)
        {
            AddCorners(ref definition);

            var partitionData = new PartitionData
            {
                IsDirty = readOnlyCameraSamplingData.IsDirty, RawSqrDistance = -1
            };
            partitionDataContainer.SetPartitionData(ref partitionData);
        }

        protected void AddCorners(ref SceneDefinitionComponent definition)
        {
            var corners = new NativeArray<ParcelCorners>(definition.ParcelsCorners.Count, Allocator.Persistent);

            for (var i = 0; i < definition.ParcelsCorners.Count; i++)
                corners[i] = definition.ParcelsCorners[i];

            partitionDataContainer.AddCorners(new ParcelCornersData(in corners));
            definition.InternalJobIndex = partitionDataContainer.currentPartitionIndex;
        }

        [Query]
        private void PartitionExistingEntity(ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            if (definition.InternalJobIndex < 0) return;
            partitionDataContainer.SetPartitionComponentData(definition.InternalJobIndex, ref partitionComponent);
        }

    }
}
