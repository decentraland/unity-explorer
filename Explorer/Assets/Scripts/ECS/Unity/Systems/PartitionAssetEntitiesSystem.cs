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
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        private static ProfilerMarker job_PrepareMarker = new ("PartitionAssetEntitiesSystem.Job_Prepare");
        private static ProfilerMarker job_RunMarker = new ("PartitionAssetEntitiesSystem.Job_Run");
        private static ProfilerMarker job_ResultMarker = new ("PartitionAssetEntitiesSystem.Job_Result");
        private readonly IReadOnlyCameraSamplingData samplingData;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly Entity sceneRoot;

        private readonly IPartitionSettings partitionSettings;
        private readonly IPartitionComponent scenePartition;

        private readonly NativeArray<int> sqrDistanceBuckets;

        private JobHandle handle;
        private int idInJob;

        private NativeList<byte> bucket = new (1024, Allocator.Persistent);
        private NativeList<bool> isBehind = new (1024, Allocator.Persistent);
        private NativeList<Vector3> position = new (1024, Allocator.Persistent);

        private readonly HashSet<(Repartitionable, PartitionComponent)> repartitionableSet = new ();

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

            sqrDistanceBuckets = new NativeArray<int>(partitionSettings.SqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < partitionSettings.SqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];
        }

        //  protected override void Update(float t)
        // {
        //     idInJob = 0;
        //
        //     Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
        //
        //     if (samplingData.IsDirty) // Camera has moved
        //         RepartitionAllEntitiesQuery(World, scenePosition);
        //     else
        //     {
        //         ResetEntitiesWithoutTransformQuery(World);
        //         RepartitionEntitiesWithTransformQuery(World);
        //     }
        //
        //     PartitionNewEntitiesQuery(World, scenePosition);
        //
        //     if (idInJob == 0)
        //         return;
        //
        //     PartitionJob partitionJob;
        //
        //     using (job_PrepareMarker.Auto())
        //         partitionJob = new PartitionJob
        //         {
        //             FastPathSqrDistance = partitionSettings.FastPathSqrDistance,
        //             SqrDistanceBuckets = sqrDistanceBuckets,
        //             CameraPosition = samplingData.Position,
        //             CameraForward = samplingData.Forward,
        //             SceneBucket = scenePartition.Bucket,
        //             SceneIsBehind = scenePartition.IsBehind,
        //
        //             Bucket = bucket,
        //             IsBehind = isBehind,
        //             Position = position,
        //         };
        //
        //     using (job_RunMarker.Auto())
        //     {
        //         handle = partitionJob.Schedule(idInJob, 64);
        //         handle.Complete();
        //     }
        //
        //     using (job_ResultMarker.Auto())
        //     {
        //         foreach ((Repartitionable repartitionable, PartitionComponent partitionComponent) in repartitionableSet)
        //             if (repartitionable.IdInJob > 0 && repartitionable.IdInJob < bucket.Length)
        //             {
        //                 partitionComponent.Bucket = bucket[repartitionable.IdInJob];
        //                 partitionComponent.IsBehind =  isBehind[repartitionable.IdInJob];
        //
        //                 partitionComponent.IsDirty = repartitionable.IsNewEntity
        //                                              || partitionComponent.Bucket != bucket[repartitionable.IdInJob]
        //                                              || partitionComponent.IsBehind != isBehind[repartitionable.IdInJob];
        //             }
        //
        //         repartitionableSet.Clear();
        //         bucket.Clear();
        //         isBehind.Clear();
        //         position.Clear();
        //     }
        // }


        protected override void Update(float t)
        {
            // First re-partition if player position or rotation is changed
            // if is true then re-partition if Transform.isDirty

            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            if (samplingData.IsDirty)
            {
                // Repartition everything
                RePartitionExistingEntityQuery(World, cameraPosition, cameraForward, false);
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

        private void RePartition(Vector3 cameraTransform, Vector3 cameraForward, Vector3 entityPosition, ref PartitionComponent partitionComponent)
        {
            // TODO pure math logic can be jobified for much better performance

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
            else ResolvePartitionFromDistance(partitionSettings, cameraForward, partitionComponent, sqrDistance, vectorToCamera);

            partitionComponent.IsDirty = bucket != partitionComponent.Bucket || isBehind != partitionComponent.IsBehind;
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
        }


        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))] // PbMaterial is attached to the renderer and can contain textures
        [None(typeof(Repartitionable), typeof(PartitionComponent))]
        private void PartitionNewEntities(in Entity entity, [Data] Vector3 scenePosition)
        {
            Repartitionable repartitionable = new Repartitionable();
            PartitionComponent partitionComponent = partitionComponentPool.Get();

            var position = World.TryGet(entity, out TransformComponent transformComponent)
                ?transformComponent.Cached.WorldPosition
                : scenePosition;

            AddToJob(ref repartitionable, partitionComponent, position);

            repartitionable.IsNewEntity = true;
            World.Add(entity, partitionComponent, repartitionable);
        }

        [Query]
        private void RepartitionAllEntities(in Entity entity, ref Repartitionable repartitionable, ref PartitionComponent partitionComponent, [Data] Vector3 scenePosition)
        {
            var position = World.TryGet(entity, out TransformComponent transformComponent)
                ? transformComponent.Cached.WorldPosition
                : scenePosition;

            AddToJob(ref repartitionable, partitionComponent, position);
        }

        [Query]
        private void RepartitionEntitiesWithTransform(ref SDKTransform sdkTransform, ref TransformComponent transformComponent, ref Repartitionable repartitionable, ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
            repartitionable.IdInJob = 0;

            if (sdkTransform.IsDirty)
                AddToJob(ref repartitionable, partitionComponent, transformComponent.Cached.WorldPosition);
        }

        [Query]
        [None(typeof(SDKTransform))]
        private void ResetEntitiesWithoutTransform(ref Repartitionable repartitionable, ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
            repartitionable.IdInJob = 0;
        }

        private void AddToJob(ref Repartitionable repartitionable, PartitionComponent partitionComponent, Vector3 inPosition)
        {
            repartitionableSet.Add((repartitionable, partitionComponent));

            repartitionable.IsNewEntity = false;
            repartitionable.IdInJob = idInJob;

            bucket.Add(partitionComponent.Bucket);
            isBehind.Add(partitionComponent.IsBehind);
            position.Add(inPosition);

            idInJob++;
        }

        [BurstCompile]
        public struct PartitionJob : IJobParallelFor
        {
            public int FastPathSqrDistance;
            [ReadOnly] public NativeArray<int> SqrDistanceBuckets;

            public byte SceneBucket;
            public bool SceneIsBehind;

            public Vector3 CameraPosition;
            public Vector3 CameraForward;

            [ReadOnly] public NativeArray<Vector3> Position;

            public NativeArray<byte> Bucket;
            public NativeArray<bool> IsBehind;

            public void Execute(int index)
            {
                Vector3 vectorToCamera = Position[index] - CameraPosition;
                float sqrDistance = Vector3.SqrMagnitude(vectorToCamera);

                if (sqrDistance > FastPathSqrDistance)
                {
                    Bucket[index] = SceneBucket;
                    IsBehind[index] = SceneIsBehind;
                }
                else
                {
                    // Find the bucket
                    byte bucketIndex = 0;
                    while (bucketIndex < SqrDistanceBuckets.Length && sqrDistance >= SqrDistanceBuckets[bucketIndex])
                        bucketIndex++;

                    Bucket[index] = bucketIndex;
                    IsBehind[index] = Vector3.Dot(CameraForward, vectorToCamera) < 0;
                }
            }
        }
    }

    public struct Repartitionable
    {
        public int IdInJob;
        public bool IsNewEntity;
    }
}
