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
using UnityEngine;
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
        public const int DEPLOYED_SCENES_LIMIT = 90000; // 300x300 scenes (without empty)

        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly IComponentPool<InternalJobIndexComponent> internalJobIndexPool;
        private readonly IReadOnlyCameraSamplingData readOnlyCameraSamplingData;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        internal readonly PartitionDataContainer partitionDataContainer;

        private JobHandle partitionJobHandle;
        private bool isRunningJob;

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IComponentPool<InternalJobIndexComponent> internalJobIndexPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData,
            PartitionDataContainer partitionDataContainer,
            IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.internalJobIndexPool = internalJobIndexPool;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;
            this.partitionDataContainer = partitionDataContainer;
            this.realmPartitionSettings = realmPartitionSettings;

            partitionDataContainer.Initialize(DEPLOYED_SCENES_LIMIT, partitionSettings.SqrDistanceBuckets, partitionSettings);
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
                PartitionScheduledDefinitionQuery(World);
                PartitionNewEntityQuery(World);
            }

            // Repartition if camera transform is qualified and the last job has already been completed
            if (readOnlyCameraSamplingData.IsDirty && !isRunningJob && partitionDataContainer.CurrentPartitionIndex > 0)
            {
                float unloadingDistance = (Mathf.Max(1, realmPartitionSettings.UnloadingDistanceToleranceInParcels) + realmPartitionSettings.MaxLoadingDistanceInParcels)
                                          * PARCEL_SIZE;
                float unloadingSqrDistance = unloadingDistance * unloadingDistance;
                partitionJobHandle = partitionDataContainer.ScheduleJob(readOnlyCameraSamplingData, unloadingSqrDistance);
                isRunningJob = true;
            }
        }

        [Query]
        [None(typeof(PartitionComponent), typeof(InternalJobIndexComponent))]
        private void PartitionNewEntity(in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (definition.IsPortableExperience)
            {
                PartitionComponent partitionComponent = partitionComponentPool.Get();
                partitionComponent.OutOfRange = false;
                partitionComponent.Bucket = 0;
                partitionComponent.IsBehind = false;
                partitionComponent.RawSqrDistance = 1;
                partitionComponent.IsDirty = true;
                World.Add(entity, partitionComponent);
                return;
            }

            ScheduleSceneDefinition(ref definition, out InternalJobIndexComponent internalJobIndex);
            World.Add(entity, internalJobIndex);
        }

        [Query]
        [All(typeof(InternalJobIndexComponent))]
        [None(typeof(PartitionComponent))]
        private void PartitionScheduledDefinition(in Entity entity, InternalJobIndexComponent internalJobIndex)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();

            partitionDataContainer.SetPartitionComponentData(internalJobIndex.InternalJobIndex,
                ref partitionComponent);

            World.Add(entity, partitionComponent);
        }

        private void ScheduleSceneDefinition(ref SceneDefinitionComponent definition,
            out InternalJobIndexComponent internalJobIndex)
        {
            AddCorners(ref definition, out internalJobIndex);

            var partitionData = new PartitionData
            {
                IsDirty = readOnlyCameraSamplingData.IsDirty, RawSqrDistance = -1,
            };

            partitionDataContainer.SetPartitionData(partitionData);
        }

        private void AddCorners(ref SceneDefinitionComponent definition,
            out InternalJobIndexComponent internalJobIndex)
        {
            var corners = new NativeArray<ParcelCorners>(definition.ParcelsCorners.Count, Allocator.Persistent);

            for (var i = 0; i < definition.ParcelsCorners.Count; i++)
                corners[i] = definition.ParcelsCorners[i];

            partitionDataContainer.AddCorners(new ParcelCornersData(in corners));
            internalJobIndex = internalJobIndexPool.Get();

            if (internalJobIndex.InternalJobIndex < 0)
                internalJobIndex.InternalJobIndex = partitionDataContainer.CurrentPartitionIndex;
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        [None(typeof(PortableExperienceComponent))]
        private void PartitionExistingEntity(PartitionComponent partitionComponent,
            InternalJobIndexComponent internalJobIndex)
        {
            partitionDataContainer.SetPartitionComponentData(internalJobIndex.InternalJobIndex,
                ref partitionComponent);
        }
    }
}
