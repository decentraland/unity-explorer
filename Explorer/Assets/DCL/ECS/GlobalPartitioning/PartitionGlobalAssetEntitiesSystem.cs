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
using System.Runtime.CompilerServices;
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

        private readonly NativeArray<int> partitionSqrDistanceBuckets;

        internal PartitionGlobalAssetEntitiesSystem(World world, IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings settings, IReadOnlyCameraSamplingData cameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            samplingData = cameraSamplingData;

            partitionSqrDistanceBuckets = new NativeArray<int>(settings.SqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < settings.SqrDistanceBuckets.Count; i++)
                partitionSqrDistanceBuckets[i] = settings.SqrDistanceBuckets[i];
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

        private void RePartition(Vector3 cameraPosition, Vector3 cameraForward, Vector3 entityPosition, ref PartitionComponent partitionComponent)
        {
            var isBehindResult = new NativeArray<bool>(1, Allocator.Temp);
            var bucketIndexResult = new NativeArray<byte>(1, Allocator.Temp);

            var isBehindJob = new PartitionJob
            {
                CameraForward = cameraForward,
                EntityPosition = entityPosition,
                CameraTransform = cameraPosition,
                SqrDistanceBuckets = partitionSqrDistanceBuckets,

                BucketIndexResult = bucketIndexResult,
                IsBehindResult = isBehindResult,
            };

            JobHandle isBehindJobHandle = isBehindJob.Schedule();
            isBehindJobHandle.Complete();
            bool isBehind = isBehindResult[0];
            byte bucketIndex = bucketIndexResult[0];

            bucketIndexResult.Dispose();
            isBehindResult.Dispose();

            partitionComponent.IsDirty = partitionComponent.IsBehind != isBehind || partitionComponent.Bucket != bucketIndex;

            partitionComponent.IsBehind = isBehind;
            partitionComponent.Bucket = bucketIndex;
        }
    }

    public struct PartitionJob : IJob
    {
        [ReadOnly] public Vector3 CameraForward;
        [ReadOnly] public Vector3 CameraTransform;
        [ReadOnly] public Vector3 EntityPosition;

        [ReadOnly] public NativeArray<int> SqrDistanceBuckets;

        public NativeArray<bool> IsBehindResult;
        public NativeArray<byte> BucketIndexResult;

        public void Execute()
        {
            Vector3 vectorToCamera = EntityPosition - CameraTransform;

            IsBehindResult[0] = Vector3.Dot(CameraForward, vectorToCamera) < 0;
            BucketIndexResult[0] = CalculateIndex(Vector3.SqrMagnitude(vectorToCamera));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte CalculateIndex(float sqrDistance)
        {
            for (byte bucketIndex = 0; bucketIndex < SqrDistanceBuckets.Length; bucketIndex++)
                if (sqrDistance < SqrDistanceBuckets[bucketIndex])
                    return bucketIndex;

            return 0;
        }
    }
}
