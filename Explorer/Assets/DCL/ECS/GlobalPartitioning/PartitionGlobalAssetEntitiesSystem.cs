using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Unity.Systems;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

// ReSharper disable once CheckNamespace (Code generation issues)
namespace DCL.Systems
{
    /// <summary>
    ///     Similar to <see cref="PartitionAssetEntitiesSystem" /> but partitions components for
    ///     qualified entities that exist in the global world.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PartitionGlobalAssetEntitiesSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyCameraSamplingData samplingData;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;

        private readonly NativeArray<int> partiotionSqrDistanceBuckets;

        internal PartitionGlobalAssetEntitiesSystem(World world, IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings settings, IReadOnlyCameraSamplingData cameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            samplingData = cameraSamplingData;

            partiotionSqrDistanceBuckets = new NativeArray<int>(settings.SqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < settings.SqrDistanceBuckets.Count; i++)
                partiotionSqrDistanceBuckets[i] = settings.SqrDistanceBuckets[i];
        }

        protected override void Update(float t)
        {
            // First re-partition if player position or rotation is changed
            // if is true then re-partition if Transform.isDirty

            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            if (samplingData.IsDirty)
            {
                // Repartition everything
                RePartitionExistingEntityQuery(World, cameraPosition, cameraForward);
            }
            else
            {
                ResetDirtyQuery(World);

                // Repartition all entities with dirty transform
                // TODO we don't have a scheme for changing transform in the global world at the moment
                // RePartitionExistingEntityQuery(World, cameraPosition, cameraForward, true);
            }

            // Then partition all entities that are not partitioned yet
            PartitionNewEntityQuery(World, cameraPosition, cameraForward);
        }

        [Query]
        [Any(typeof(PBAvatarShape), typeof(Profile))]
        private void ResetDirty(ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
        }

        [Query]
        [Any(typeof(PBAvatarShape), typeof(Profile))]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntity(in Entity entity, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, ref CharacterTransform transformComponent)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, transformComponent.Transform.position, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        [Any(typeof(PBAvatarShape), typeof(Profile))]
        [None(typeof(PlayerComponent))]
        private void RePartitionExistingEntity([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, ref CharacterTransform transformComponent,
            ref PartitionComponent partitionComponent)
        {
            RePartition(cameraPosition, cameraForward, transformComponent.Transform.position, ref partitionComponent);
        }

        private void RePartition(Vector3 cameraTransform, Vector3 cameraForward, Vector3 entityPosition, ref PartitionComponent partitionComponent)
        {
            Vector3 vectorToCamera = entityPosition - cameraTransform;

            // Behind
            var isBehindResult = new NativeArray<bool>(1, Allocator.Temp);

            var isBehindJob = new PartitionIsBehindJob
            {
                CameraForward = cameraForward,
                VectorToCamera = vectorToCamera,
                IsBehindResult = isBehindResult,
            };

            JobHandle isBehindJobHandle = isBehindJob.Schedule();
            isBehindJobHandle.Complete();
            bool isBehind = isBehindResult[0];
            isBehindResult.Dispose();

            if (partitionComponent.IsBehind != isBehind)
            {
                partitionComponent.IsDirty = true;
                partitionComponent.IsBehind = isBehind;
                return;
            }

            // Bucket
            var bucketIndexResult = new NativeArray<byte>(1, Allocator.Temp);

            var bucketIndexJob = new PartitionBucketJob
            {
                SqrDistanceBuckets = partiotionSqrDistanceBuckets,
                VectorToCamera = vectorToCamera,
                Result = bucketIndexResult,
            };

            JobHandle bucketIndexJobHandle = bucketIndexJob.Schedule();

            bucketIndexJobHandle.Complete();
            byte bucketIndex = bucketIndexResult[0];
            bucketIndexResult.Dispose();

            partitionComponent.IsDirty = partitionComponent.Bucket != bucketIndex;
            partitionComponent.Bucket = bucketIndex;
        }
    }

    public struct PartitionBucketJob : IJob
    {
        [ReadOnly] public NativeArray<int> SqrDistanceBuckets;
        [ReadOnly] public Vector3 VectorToCamera;

        public NativeArray<byte> Result;

        public void Execute()
        {
            float sqrDistance = Vector3.SqrMagnitude(VectorToCamera);

            for (byte bucketIndex = 0; bucketIndex < SqrDistanceBuckets.Length; bucketIndex++)
            {
                if (sqrDistance < SqrDistanceBuckets[bucketIndex])
                {
                    Result[0] = bucketIndex;
                    return;
                }
            }
        }
    }

    public struct PartitionIsBehindJob : IJob
    {
        [ReadOnly] public Vector3 CameraForward;
        [ReadOnly] public Vector3 VectorToCamera;

        public NativeArray<bool> IsBehindResult;

        public void Execute()
        {
            IsBehindResult[0] = Vector3.Dot(CameraForward, VectorToCamera) < 0;
        }
    }
}
