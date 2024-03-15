using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly IReadOnlyCameraSamplingData readOnlyCameraSamplingData;
        private readonly JobScheduler.JobScheduler jobScheduler;

        // These lists are static because of a compile issue when passing the references to the Query as [Data], code-gen wont find Unity.Collections
        protected static NativeArray<PartitionData> partitions;
        private static UnsafeList<ParcelCornersData> parcelCorners;

        private ScenePartitionParallelJob partitionJob;
        private JobHandle partitionJobHandle;
        private bool isRunningJob;
        private bool forceJobRun;
        private int currentPartitionIndex;
        private NativeArray<int> sqrDistanceBuckets;

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;

            // Genesis city goes from -150 to 150 so the max amount of partitions is always going to be 90000
            partitions = new NativeArray<PartitionData>(90000, Allocator.Persistent);
            parcelCorners = new UnsafeList<ParcelCornersData>(90000, Allocator.Persistent);

            // TODO: This might change with quality settings, consider updating them
            sqrDistanceBuckets = new NativeArray<int>(partitionSettings.SqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < partitionSettings.SqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];

            partitionJob = new ScenePartitionParallelJob(ref partitions)
            {
                SqrDistanceBuckets = sqrDistanceBuckets,
            };
        }

        public override void Dispose()
        {
            partitionJobHandle.Complete();
            partitions.Dispose();

            // not sure if the parcelCorners.Dispose() will dispose its children as well so we explicitly do so here
            foreach (ParcelCornersData cornersData in parcelCorners)
                cornersData.Dispose();

            parcelCorners.Dispose();
            sqrDistanceBuckets.Dispose();
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

                if (currentPartitionIndex > 0)
                    forceJobRun = true;
            }

            // Repartition if camera transform is qualified and the last job has already been completed
            if ((forceJobRun || readOnlyCameraSamplingData.IsDirty) && !isRunningJob && currentPartitionIndex > 0)
            {
                partitionJob.CameraForward = readOnlyCameraSamplingData.Forward;
                partitionJob.CameraPosition = readOnlyCameraSamplingData.Position;
                partitionJob.ParcelCorners = parcelCorners;
                partitionJobHandle = partitionJob.Schedule(currentPartitionIndex, 8);
                isRunningJob = true;
                forceJobRun = false;
            }
        }

        [Query]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntity(in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (definition.InternalJobIndex < 0)
            {
                ScheduleSceneDefinition(ref definition);
            }
            else
            {
                PartitionData partition = partitions[definition.InternalJobIndex];
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

            partitions[currentPartitionIndex] = new PartitionData
            {
                IsDirty = readOnlyCameraSamplingData.IsDirty,
                RawSqrDistance = -1,
            };

            currentPartitionIndex++;
        }

        protected void AddCorners(ref SceneDefinitionComponent definition)
        {
            var corners = new NativeArray<ParcelCorners>(definition.ParcelsCorners.Count, Allocator.Persistent);

            for (var i = 0; i < definition.ParcelsCorners.Count; i++)
                corners[i] = definition.ParcelsCorners[i];

            parcelCorners.Add(new ParcelCornersData(in corners));
            definition.InternalJobIndex = currentPartitionIndex;
        }

        [Query]
        private void PartitionExistingEntity(in Entity entity, ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            if (definition.InternalJobIndex < 0) return;
            PartitionData partition = partitions[definition.InternalJobIndex];
            partitionComponent.IsDirty = partition.IsDirty;
            partitionComponent.IsBehind = partition.IsBehind;
            partitionComponent.Bucket = partition.Bucket;
            partitionComponent.RawSqrDistance = partition.RawSqrDistance;
        }
    }
}
