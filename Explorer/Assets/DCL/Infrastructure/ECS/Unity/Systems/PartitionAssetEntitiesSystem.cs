using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms.Components;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace ECS.Unity.Systems
{
    /// <summary>
    ///     <para>
    ///         Runs in a scene world, modifies partition component for all entities that contain
    ///         components that can be prioritized.
    ///     </para>
    ///     <para>The execution of the group is allowed if one of the following fulfills:</para>
    ///     <para>Position or Rotation of camera is changed more than by "delta"</para>
    ///     <para>An entity that contain a qualified for partitioning component is not partitioned yet</para>
    ///     <para>Position of entity has changed</para>
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.PRIORITIZATION)]
    public partial class PartitionAssetEntitiesSystem : BaseUnityLoopSystem
    {
        private static ProfilerMarker mRePartition = new ("PartitionAssetEntitiesSystem.mRePartition");
        private static ProfilerMarker mResetDirtyQuery = new ("PartitionAssetEntitiesSystem.ResetDirtyQuery");

        private static ProfilerMarker mJobRePartitionPreparation = new ("PartitionAssetEntitiesSystem.mJobRePartitionPreparation");
        private static ProfilerMarker mJobRePartitionRun = new ("PartitionAssetEntitiesSystem.mJobRePartitionRun");
        private static ProfilerMarker mJobRePartitionApply = new ("PartitionAssetEntitiesSystem.mJobRePartitionApply");

        private readonly IReadOnlyCameraSamplingData samplingData;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly Entity sceneRoot;

        private readonly IPartitionSettings partitionSettings;
        private readonly IPartitionComponent scenePartition;

        private readonly Schedulers.JobScheduler jobScheduler;

        internal PartitionAssetEntitiesSystem(World world,
            IPartitionSettings partitionSettings,
            IPartitionComponent partition,
            IReadOnlyCameraSamplingData samplingData,
            IComponentPool<PartitionComponent> partitionComponentPool,
            Entity sceneRoot) : base(world)
        {
            this.partitionSettings = partitionSettings;
            scenePartition = partition;
            this.samplingData = samplingData;
            this.partitionComponentPool = partitionComponentPool;
            this.sceneRoot = sceneRoot;
        }

        public struct PartitionSettingsData
        {
            public byte ScenePartitionBucket;
            public bool ScenePartitionIsBehind;

            public float FastPathSqrDistance;
            public NativeArray<float> SqrDistanceBuckets;
        }

        private PartitionSettingsData partitionSettingsData;
        private NativeArray<float> sqrDistanceBucketsArray;
        private PartitionComponent[] partitions = new PartitionComponent[32];

        public override void Initialize()
        {
            base.Initialize();

            sqrDistanceBucketsArray = new NativeArray<float>(partitionSettings.SqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < partitionSettings.SqrDistanceBuckets.Count; i++)
                sqrDistanceBucketsArray[i] = partitionSettings.SqrDistanceBuckets[i];

            partitionSettingsData = new PartitionSettingsData
            {
                FastPathSqrDistance = partitionSettings.FastPathSqrDistance,
                ScenePartitionBucket = scenePartition.Bucket,
                ScenePartitionIsBehind = scenePartition.IsBehind,
                SqrDistanceBuckets = sqrDistanceBucketsArray
            };

             queryW = World.Query(in partitionsW);
             queryWo = World.Query(in partitionsWo);
        }

        private Query queryW;
        private Query queryWo;

        protected override void OnDispose()
        {
            if (sqrDistanceBucketsArray.IsCreated)
                sqrDistanceBucketsArray.Dispose();
        }


        [BurstCompile]
        public struct RePartitionJob : IJobParallelFor
        {
            [ReadOnly] public PartitionSettingsData PartitionSettings;

            [ReadOnly] public float3 CameraPosition;
            [ReadOnly] public float3 CameraForward;

            [ReadOnly] public NativeArray<float3> EntityPositions;
            [NativeDisableParallelForRestriction] public NativeArray<RepartitionableAssetEntity> Repartitionable;

            public void Execute(int index)
            {
                RepartitionableAssetEntity partition = Repartitionable[index];

                var entityPosition = EntityPositions[index];
                float3 vectorToCamera = entityPosition - CameraPosition;
                float sqrDistance = math.lengthsq(vectorToCamera);

                byte oldBucket = partition.Bucket;
                bool oldIsBehind = partition.IsBehind;

                if (sqrDistance > PartitionSettings.FastPathSqrDistance)
                {
                    partition.Bucket = PartitionSettings.ScenePartitionBucket;
                    partition.IsBehind = PartitionSettings.ScenePartitionIsBehind;
                }
                else
                {
                    byte bucketIndex = 0;

                    while (bucketIndex < PartitionSettings.SqrDistanceBuckets.Length && sqrDistance >= PartitionSettings.SqrDistanceBuckets[bucketIndex])
                        bucketIndex++;

                    partition.Bucket = bucketIndex;
                    partition.IsBehind = math.dot(CameraForward, vectorToCamera) < 0;
                }

                partition.IsDirty = oldBucket != partition.Bucket || oldIsBehind != partition.IsBehind;

                Repartitionable[index] = partition;
            }
        }

        private static ProfilerMarker mJobPrep_IterateArchetypes = new ("PartitionAssetEntitiesSystem.mJobPrep_IterateArchetypes");
        private static ProfilerMarker mJobPrep_CreateArrays = new ("PartitionAssetEntitiesSystem.mJobPrep_CreateArrays");
        private static ProfilerMarker mJobPrep_ChunkW = new ("PartitionAssetEntitiesSystem.mJobPrep_ChunkW");
        private static ProfilerMarker mJobPrep_ChunkWo = new ("PartitionAssetEntitiesSystem.mJobPrep_ChunkWo");
        private static ProfilerMarker mJobPrep_Dispose = new ("PartitionAssetEntitiesSystem.mJobPrep_Dispose");

        QueryDescription partitionsAll = new QueryDescription().WithAll<RepartitionableAssetEntity>();

        QueryDescription partitionsW = new QueryDescription().WithAll<RepartitionableAssetEntity, PartitionComponent, TransformComponent>();

        QueryDescription partitionsWo = new QueryDescription().WithAll<RepartitionableAssetEntity, PartitionComponent>()
                                                              .WithNone<TransformComponent>();

        protected override void Update(float t)
        {
            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            // First re-partition everything if player position or rotation has changed
            if (samplingData.IsDirty)
            {
                using (mRepartitionAllExistingEntityQuery.Auto())
                {
                    mJobRePartitionPreparation.Begin();

                    // TODO: split jobs and have several of them
                    var rePartitionJob = new RePartitionJob
                    {
                        CameraPosition = cameraPosition,
                        CameraForward = cameraForward,
                        PartitionSettings = partitionSettingsData,
                    };

                    mJobPrep_IterateArchetypes.Begin();
                    int totalAmount = World.CountEntities(partitionsAll);
                    mJobPrep_IterateArchetypes.End();

                    mJobPrep_CreateArrays.Begin();
                    if (partitions.Length < totalAmount) Array.Resize(ref this.partitions, totalAmount);
                    NativeArray<RepartitionableAssetEntity> repartitionables = new NativeArray<RepartitionableAssetEntity>(totalAmount, Allocator.TempJob);
                    NativeArray<float3> entityPositions = new NativeArray<float3>(totalAmount, Allocator.TempJob);
                    mJobPrep_CreateArrays.End();

                    mJobPrep_ChunkW.Begin();

                    unsafe { UnsafeUtility.MemCpyReplicate(entityPositions.GetUnsafePtr(), &scenePosition, UnsafeUtility.SizeOf<float3>(), totalAmount); }

                    int chunksOffset = 0;
                    foreach (ref Chunk chunk in queryW.GetChunkIterator()) // foreach (ref Chunk chunk in query)
                    {
                        PartitionComponent[] partitionComponents = chunk.GetArray<PartitionComponent>();
                        Array.Copy(partitionComponents, 0, partitions, chunksOffset, chunk.Size);

                        ref var repartitionableFirst = ref chunk.GetFirst<RepartitionableAssetEntity>();
                        ref var transformFirst = ref chunk.GetFirst<TransformComponent>();

                        unsafe
                        {
                            var repartitionablesPtr = (RepartitionableAssetEntity*)repartitionables.GetUnsafePtr();
                            repartitionablesPtr += chunksOffset;
                            UnsafeUtility.MemCpy(repartitionablesPtr, Unsafe.AsPointer(ref repartitionableFirst), chunk.Size * UnsafeUtility.SizeOf<RepartitionableAssetEntity>());

                            var positionsPtr = (float3*)entityPositions.GetUnsafePtr();
                            positionsPtr += chunksOffset;

                            for (var i = 0; i < chunk.Size; i++)
                                positionsPtr[i] = Unsafe.Add(ref transformFirst, i).Cached.WorldPosition;
                        }

                        chunksOffset += chunk.Size;
                    }
                    mJobPrep_ChunkW.End();

                    using (mJobPrep_ChunkWo.Auto())
                    {
                        foreach (ref Chunk chunk in queryWo.GetChunkIterator()) // foreach (ref Chunk chunk in query)
                        {
                            PartitionComponent[] partitionComponents = chunk.GetArray<PartitionComponent>();
                            Array.Copy(partitionComponents, 0, partitions, chunksOffset, chunk.Size);

                            ref var repartitionableFirst = ref chunk.GetFirst<RepartitionableAssetEntity>();

                            unsafe
                            {
                                var repartitionablesPtr = (RepartitionableAssetEntity*)repartitionables.GetUnsafePtr();
                                repartitionablesPtr += chunksOffset;

                                UnsafeUtility.MemCpy(repartitionablesPtr, Unsafe.AsPointer(ref repartitionableFirst), chunk.Size * UnsafeUtility.SizeOf<RepartitionableAssetEntity>());
                            }

                            chunksOffset += chunk.Size;
                        }
                    }

                    rePartitionJob.EntityPositions = entityPositions;
                    rePartitionJob.Repartitionable = repartitionables;
                    mJobRePartitionPreparation.End();

                    mJobRePartitionRun.Begin();
                    JobHandle jobHandle = rePartitionJob.Schedule(repartitionables.Length, 64);
                    jobHandle.Complete();
                    mJobRePartitionRun.End();

                    mJobRePartitionApply.Begin();

                    for (int i = 0; i < repartitionables.Length; i++)
                    {
                        partitions[i].Bucket = repartitionables[i].Bucket;
                        partitions[i].IsBehind = repartitionables[i].IsBehind;
                        partitions[i].IsDirty = repartitionables[i].IsDirty;
                    }

                    mJobRePartitionApply.End();

                    mJobPrep_Dispose.Begin();
                    Array.Clear(partitions, 0, partitions.Length);
                    repartitionables.Dispose();
                    entityPositions.Dispose();
                    mJobPrep_Dispose.End();
                }

                // using (mRepartitionExistingEntityWithoutTransformQ.Auto())
                //     RepartitionExistingEntityWithoutTransformQuery(World, scenePosition, cameraPosition, cameraForward);
                //
                // using (mRePartitionExistingEntityWithTransformQ.Auto())
                //     RePartitionExistingEntityWithTransformQuery(World, cameraPosition, cameraForward);
            }
            else // re-partition if Transform.isDirty
            {
                using (mResetDirtyQuery.Auto())
                    ResetDirtyQuery(World);

                // Repartition all entities with dirty transform
                using(mRePartitionExistingEntityQuery.Auto())
                    RePartitionExistingEntityWithDirtySdkTransformQuery(World, cameraPosition, cameraForward);
            }

            using (mPartitionNewEntityQuery.Auto())
            { // Then partition all entities that are not partitioned yet
                PartitionNewEntityWithoutTransformQuery(World, scenePosition, cameraPosition, cameraForward);
                PartitionNewEntityWithTransformQuery(World, cameraPosition, cameraForward);
            }
        }

        private static ProfilerMarker mRePartitionExistingEntityQuery = new ("PartitionAssetEntitiesSystem.RePartitionExistingEntityQuery");
        private static ProfilerMarker mPartitionNewEntityQuery = new ("PartitionAssetEntitiesSystem.PartitionNewEntityQuery");
        private static ProfilerMarker mRepartitionAllExistingEntityQuery = new ("PartitionAssetEntitiesSystem.RepartitionAllEntityQuery");


        [Query]
        [All(typeof(RepartitionableAssetEntity))]
        private void ResetDirty(ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))] // PbMaterial is attached to the renderer and can contain textures
        [None(typeof(TransformComponent), typeof(PartitionComponent))]
        private void PartitionNewEntityWithoutTransform([Data] Vector3 scenePosition, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, in Entity entity)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, scenePosition, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent, new RepartitionableAssetEntity());
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))] // PbMaterial is attached to the renderer and can contain textures
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntityWithTransform(ref TransformComponent transformComponent, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, in Entity entity)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, transformComponent.Cached.WorldPosition, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent, new RepartitionableAssetEntity());
        }

        // [Query]
        // [All(typeof(RepartitionableAssetEntity))]
        // private void RePartitionExistingEntityWithTransform(ref PartitionComponent partitionComponent, ref TransformComponent transformComponent, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward)
        // {
        //     RePartition(cameraPosition, cameraForward, transformComponent.Cached.WorldPosition, ref partitionComponent);
        // }

        // [Query]
        // [All(typeof(RepartitionableAssetEntity))]
        // [None(typeof(TransformComponent))]
        // private void RepartitionExistingEntityWithoutTransform(ref PartitionComponent partitionComponent, [Data] Vector3 scenePosition, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward)
        // {
        //     RePartition(cameraPosition, cameraForward, scenePosition, ref partitionComponent);
        // }

        [Query]
        [All(typeof(RepartitionableAssetEntity))]
        private void RePartitionExistingEntityWithDirtySdkTransform(ref PartitionComponent partitionComponent, ref SDKTransform sdkTransform, ref TransformComponent transformComponent, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward)
        {
            if (sdkTransform.IsDirty)
                RePartition(cameraPosition, cameraForward, transformComponent.Cached.WorldPosition, ref partitionComponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RePartition(Vector3 cameraTransform, Vector3 cameraForward, Vector3 entityPosition, ref PartitionComponent partitionComponent)
        {
            mRePartition.Begin();
            byte bucket = partitionComponent.Bucket;
            bool isBehind = partitionComponent.IsBehind;

            // check if fast path should be used
            Vector3 vectorToCamera = entityPosition - cameraTransform;
            float sqrDistance = Vector3.SqrMagnitude(vectorToCamera);

            if (sqrDistance > partitionSettings.FastPathSqrDistance)
            {
                // just inherit Scene's values
                partitionComponent.Bucket = scenePartition.Bucket;
                partitionComponent.IsBehind = scenePartition.IsBehind;
            }
            else
            {
                partitionComponent.Bucket = BinarySearchForBucket(sqrDistance);
                partitionComponent.IsBehind = Vector3.Dot(cameraForward, vectorToCamera) < 0;
            }

            partitionComponent.IsDirty = bucket != partitionComponent.Bucket || isBehind != partitionComponent.IsBehind;
            mRePartition.End();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte BinarySearchForBucket(float sqrDistance)
        {
            var left = 0;
            int right = partitionSettings.SqrDistanceBuckets.Count - 1;
            byte bucketIndex = 0;

            while (left <= right)
            {
                int mid = (left + right) >> 1; // Same as / 2 but faster

                if (partitionSettings.SqrDistanceBuckets[mid] <= sqrDistance)
                {
                    bucketIndex = (byte)(mid + 1);
                    left = mid + 1;
                }
                else { right = mid - 1; }
            }

            return bucketIndex;
        }

        public struct RepartitionableAssetEntity
        {
            public bool IsBehind;
            public byte Bucket;
            public bool IsDirty;
        }
    }
}
