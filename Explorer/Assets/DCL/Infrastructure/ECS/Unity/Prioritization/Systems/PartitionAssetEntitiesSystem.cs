using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
        private const int INITIAL_CAPACITY = 256;
        private const int JOB_BATCH = 64;

        private readonly IReadOnlyCameraSamplingData samplingData;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly Entity sceneRoot;

        private readonly IPartitionSettings partitionSettings;
        private readonly IPartitionComponent scenePartition;

        private float3[] stagedPositions;
        private PartitionComponent[] stagedComponents;
        private int stagedCount;

        private NativeArray<float3> entityPositions;
        private NativeArray<float> rawIn;
        private NativeArray<byte> outBucket;
        private NativeArray<bool> outIsBehind;
        private NativeArray<float> outRaw;
        private NativeArray<int> sqrBuckets;

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

            stagedPositions = new float3[INITIAL_CAPACITY];
            stagedComponents = new PartitionComponent[INITIAL_CAPACITY];
            entityPositions = new NativeArray<float3>(INITIAL_CAPACITY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rawIn = new NativeArray<float>(INITIAL_CAPACITY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outBucket = new NativeArray<byte>(INITIAL_CAPACITY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outIsBehind = new NativeArray<bool>(INITIAL_CAPACITY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outRaw = new NativeArray<float>(INITIAL_CAPACITY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        protected override void OnDispose()
        {
            if (entityPositions.IsCreated) entityPositions.Dispose();
            if (rawIn.IsCreated) rawIn.Dispose();
            if (outBucket.IsCreated) outBucket.Dispose();
            if (outIsBehind.IsCreated) outIsBehind.Dispose();
            if (outRaw.IsCreated) outRaw.Dispose();
            if (sqrBuckets.IsCreated) sqrBuckets.Dispose();
        }

        protected override void Update(float t)
        {
            // First re-partition if player position or rotation is changed
            // if is true then re-partition if Transform.isDirty

            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            RefreshBuckets();

            if (samplingData.IsDirty)
            {
                RePartitionAllExistingEntities(cameraPosition, cameraForward);
                RepartitionExistingEntityWithoutTransformQuery(World, scenePosition, cameraPosition, cameraForward);
            }
            else
            {
                ResetDirtyQuery(World);

                // Repartition all entities with dirty transform
                RePartitionExistingEntityQuery(World, cameraPosition, cameraForward, true);
            }

            // Then partition all entities that are not partitioned yet
            PartitionNewEntityQuery(World, cameraPosition, cameraForward);
            PartitionNewEntityWithoutTransformQuery(World, scenePosition, cameraPosition, cameraForward);
        }

        private void RefreshBuckets()
        {
            var buckets = partitionSettings.SqrDistanceBuckets;

            if (!sqrBuckets.IsCreated || sqrBuckets.Length != buckets.Count)
            {
                if (sqrBuckets.IsCreated)
                    sqrBuckets.Dispose();

                sqrBuckets = new NativeArray<int>(buckets.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            for (var i = 0; i < buckets.Count; i++)
                sqrBuckets[i] = buckets[i];
        }

        /// <summary>
        ///     Stages every existing partitioned entity (camera-moved full sweep), runs the partition
        ///     math on Burst worker threads, then writes the results back into the (class)
        ///     PartitionComponent instances. The job is completed before this method returns, so all
        ///     downstream consumers in the same synced group observe the up-to-date values.
        /// </summary>
        private void RePartitionAllExistingEntities(float3 cameraPosition, float3 cameraForward)
        {
            stagedCount = 0;
            StageExistingEntityQuery(World);

            if (stagedCount == 0)
                return;

            EnsureNativeCapacity(stagedCount);

            for (var i = 0; i < stagedCount; i++)
            {
                entityPositions[i] = stagedPositions[i];
                rawIn[i] = stagedComponents[i].RawSqrDistance;
            }

            var job = new PartitionJob
            {
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                FastPathSqrDistance = partitionSettings.FastPathSqrDistance,
                SceneBucket = scenePartition.Bucket,
                SceneIsBehind = scenePartition.IsBehind,
                SqrDistanceBuckets = sqrBuckets,
                EntityPositions = entityPositions,
                RawIn = rawIn,
                OutBucket = outBucket,
                OutIsBehind = outIsBehind,
                OutRaw = outRaw,
            };

            job.Schedule(stagedCount, JOB_BATCH).Complete();

            for (var i = 0; i < stagedCount; i++)
            {
                PartitionComponent partitionComponent = stagedComponents[i];

                byte oldBucket = partitionComponent.Bucket;
                bool oldIsBehind = partitionComponent.IsBehind;

                partitionComponent.Bucket = outBucket[i];
                partitionComponent.IsBehind = outIsBehind[i];
                partitionComponent.RawSqrDistance = outRaw[i];
                partitionComponent.IsDirty = oldBucket != partitionComponent.Bucket || oldIsBehind != partitionComponent.IsBehind;

                stagedComponents[i] = null;
            }
        }

        private void EnsureNativeCapacity(int required)
        {
            if (entityPositions.Length >= required)
                return;

            int newCap = math.max(entityPositions.Length * 2, required);

            entityPositions.Dispose();
            rawIn.Dispose();
            outBucket.Dispose();
            outIsBehind.Dispose();
            outRaw.Dispose();

            entityPositions = new NativeArray<float3>(newCap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rawIn = new NativeArray<float>(newCap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outBucket = new NativeArray<byte>(newCap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outIsBehind = new NativeArray<bool>(newCap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outRaw = new NativeArray<float>(newCap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        private void StageExistingEntity(ref SDKTransform sdkTransform, ref TransformComponent transformComponent, ref PartitionComponent partitionComponent)
        {
            if (stagedCount >= stagedComponents.Length)
            {
                int newCap = stagedComponents.Length * 2;
                System.Array.Resize(ref stagedComponents, newCap);
                System.Array.Resize(ref stagedPositions, newCap);
            }

            stagedPositions[stagedCount] = transformComponent.Cached.WorldPosition;
            stagedComponents[stagedCount] = partitionComponent;
            stagedCount++;
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]// PbMaterial is attached to the renderer and can contain textures
        private void ResetDirty(ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntity([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, in Entity entity, ref TransformComponent transformComponent)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, transformComponent.Cached.WorldPosition, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        [None(typeof(TransformComponent), typeof(PartitionComponent))]
        private void PartitionNewEntityWithoutTransform([Data] Vector3 scenePosition, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, in Entity entity)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, scenePosition, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        [None(typeof(TransformComponent))]
        private void RepartitionExistingEntityWithoutTransform([Data] Vector3 scenePosition, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, ref PartitionComponent partitionComponent)
        {
            RePartition(cameraPosition, cameraForward, scenePosition, ref partitionComponent);
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        private void RePartitionExistingEntity([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, [Data] bool checkTransform,
            ref SDKTransform sdkTransform, ref TransformComponent transformComponent, ref PartitionComponent partitionComponent)
        {
            if (checkTransform && !sdkTransform.IsDirty)
                return;

            RePartition(cameraPosition, cameraForward, transformComponent.Cached.WorldPosition, ref partitionComponent);
        }

        private void RePartition(float3 cameraTransform, float3 cameraForward, float3 entityPosition, ref PartitionComponent partitionComponent)
        {
            byte oldBucket = partitionComponent.Bucket;
            bool oldIsBehind = partitionComponent.IsBehind;

            ComputePartition(entityPosition, cameraTransform, cameraForward,
                partitionSettings.FastPathSqrDistance, sqrBuckets,
                scenePartition.Bucket, scenePartition.IsBehind, partitionComponent.RawSqrDistance,
                out byte bucket, out bool isBehind, out float raw);

            partitionComponent.Bucket = bucket;
            partitionComponent.IsBehind = isBehind;
            partitionComponent.RawSqrDistance = raw;
            partitionComponent.IsDirty = oldBucket != bucket || oldIsBehind != isBehind;
        }

        /// <summary>
        ///     Pure, Burst-compatible partition math shared verbatim by the job and the managed
        ///     fallback. Mirrors RePartition/ResolvePartitionFromDistance's evaluation order exactly
        ///     (component-wise subtract, left-to-right square sum, int bucket compare, dot-product
        ///     behind test) so both paths agree bit-for-bit. On the fast path the raw square distance
        ///     is intentionally left untouched (passed through <paramref name="rawIn"/>) — far entities
        ///     inherit the scene bucket without rewriting RawSqrDistance.
        /// </summary>
        public static void ComputePartition(
            float3 entityPosition, float3 cameraPosition, float3 cameraForward,
            float fastPathSqrDistance, NativeArray<int> sqrBuckets,
            byte sceneBucket, bool sceneIsBehind, float rawIn,
            out byte bucket, out bool isBehind, out float raw)
        {
            float vx = entityPosition.x - cameraPosition.x;
            float vy = entityPosition.y - cameraPosition.y;
            float vz = entityPosition.z - cameraPosition.z;
            float sqrDistance = (vx * vx) + (vy * vy) + (vz * vz);

            if (sqrDistance > fastPathSqrDistance)
            {
                // just inherit Scene's values
                bucket = sceneBucket;
                isBehind = sceneIsBehind;
                raw = rawIn;
                return;
            }

            byte bucketIndex;

            for (bucketIndex = 0; bucketIndex < sqrBuckets.Length; bucketIndex++)
            {
                if (sqrDistance < sqrBuckets[bucketIndex])
                    break;
            }

            bucket = bucketIndex;

            isBehind = ((cameraForward.x * vx) + (cameraForward.y * vy) + (cameraForward.z * vz)) < 0f;
            raw = sqrDistance;
        }

        public static void ResolvePartitionFromDistance(IPartitionSettings partitionSettings, Vector3 cameraForward, PartitionComponent partitionComponent,
            float sqrDistance, Vector3 vectorToCamera)
        {
            // Find the bucket
            byte bucketIndex;

            for (bucketIndex = 0; bucketIndex < partitionSettings.SqrDistanceBuckets.Count; bucketIndex++)
            {
                if (sqrDistance < partitionSettings.SqrDistanceBuckets[bucketIndex])
                    break;
            }

            partitionComponent.Bucket = bucketIndex;

            // Is behind is a dot product
            // mind that taking cosines is not cheap
            partitionComponent.IsBehind = Vector3.Dot(cameraForward, vectorToCamera) < 0;
            partitionComponent.RawSqrDistance = sqrDistance;
        }

        [BurstCompile(FloatMode = FloatMode.Strict)]
        private struct PartitionJob : IJobParallelFor
        {
            public float3 CameraPosition;
            public float3 CameraForward;
            public float FastPathSqrDistance;
            public byte SceneBucket;
            public bool SceneIsBehind;

            [ReadOnly] public NativeArray<int> SqrDistanceBuckets;
            [ReadOnly] public NativeArray<float3> EntityPositions;
            [ReadOnly] public NativeArray<float> RawIn;

            [WriteOnly] public NativeArray<byte> OutBucket;
            [WriteOnly] public NativeArray<bool> OutIsBehind;
            [WriteOnly] public NativeArray<float> OutRaw;

            public void Execute(int index)
            {
                ComputePartition(EntityPositions[index], CameraPosition, CameraForward,
                    FastPathSqrDistance, SqrDistanceBuckets, SceneBucket, SceneIsBehind, RawIn[index],
                    out byte bucket, out bool isBehind, out float raw);

                OutBucket[index] = bucket;
                OutIsBehind[index] = isBehind;
                OutRaw[index] = raw;
            }
        }
    }
}
