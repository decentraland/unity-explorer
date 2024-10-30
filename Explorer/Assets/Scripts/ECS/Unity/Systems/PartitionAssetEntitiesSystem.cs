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
        private readonly IReadOnlyCameraSamplingData samplingData;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly Entity sceneRoot;

        private readonly IPartitionSettings partitionSettings;
        private readonly IPartitionComponent scenePartition;

        private readonly NativeArray<int> sqrDistanceBuckets;

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

        private JobHandle handle;
        private int idInJob;

        private NativeList<byte> bucket = new (Allocator.Persistent);
        private NativeList<bool> isBehind = new (Allocator.Persistent);
        private NativeList<Vector3> position = new (Allocator.Persistent);

        private static ProfilerMarker job_PrepareMarker = new ("PartitionAssetEntitiesSystem.Job_Prepare");
        private static ProfilerMarker job_RunMarker = new ("PartitionAssetEntitiesSystem.Job_Run");
        private static ProfilerMarker job_ResultMarker = new ("PartitionAssetEntitiesSystem.Job_Result");

        protected override void Update(float t)
        {
            if(idInJob > 0)
            {
                handle.Complete();

                job_ResultMarker.Begin();
                ApplyPartitionQuery(World);

                bucket.Clear();
                isBehind.Clear();
                position.Clear();
                job_ResultMarker.End();
            }

            RequestRepartitionForAllEntitiesQuery(World, samplingData.IsDirty);

            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            MarkNewEntitiesForPartitionQuery(World, scenePosition);

            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            idInJob = 0;
            RepartitionAllQuery(World);

            if (idInJob == 0)
                return;

            job_PrepareMarker.Begin();
            PartitionJob partitionJob = new PartitionJob
            {
                FastPathSqrDistance = partitionSettings.FastPathSqrDistance,
                SqrDistanceBuckets = sqrDistanceBuckets,
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                SceneBucket = scenePartition.Bucket,
                SceneIsBehind = scenePartition.IsBehind,

                Bucket = bucket,
                IsBehind = isBehind,
                Position = position,
            };
            job_PrepareMarker.End();

            handle = partitionJob.Schedule(idInJob, 64);
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
                    byte bucketIndex;
                    for (bucketIndex = 0; bucketIndex < SqrDistanceBuckets.Length; bucketIndex++)
                        if (sqrDistance < SqrDistanceBuckets[bucketIndex])
                            break;

                    Bucket[index] = bucketIndex;
                    IsBehind[index] = Vector3.Dot(CameraForward, vectorToCamera) < 0;
                }
            }
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))] // PbMaterial is attached to the renderer and can contain textures
        [None(typeof(Repartitionable), typeof(PartitionComponent))]
        private void MarkNewEntitiesForPartition(in Entity entity, [Data] Vector3 scenePosition)
        {
            bool hasTransform = World.Has<TransformComponent>(entity);

            Vector3 position = hasTransform
                ? World.Get<TransformComponent>(entity).Cached.WorldPosition
                : scenePosition;

            PartitionComponent partitionComponent = partitionComponentPool.Get();

            World.Add(entity,
                partitionComponent,
                new Repartitionable { InPosition = position, IdInJob = -1, IsNewEntity = true, HasTransform = hasTransform, PartitionRequested = true });
        }

        [Query]
        private void ApplyPartition(ref Repartitionable repartitionable, ref PartitionComponent partitionComponent)
        {
            if (repartitionable.IdInJob > 0)
            {
                partitionComponent.IsDirty = partitionComponent.Bucket != bucket[repartitionable.IdInJob] || partitionComponent.IsBehind != isBehind[repartitionable.IdInJob];
                partitionComponent.Bucket = bucket[repartitionable.IdInJob];
                partitionComponent.IsBehind = isBehind[repartitionable.IdInJob];

                repartitionable.IdInJob = -1;
            }
        }

        [Query]
        private void RequestRepartitionForAllEntities(in Entity entity, ref Repartitionable repartitionable, ref PartitionComponent partitionComponent, [Data] bool playerTransformChanged)
        {
            if (playerTransformChanged || (World.Has<SDKTransform>(entity) && World.Get<SDKTransform>(entity).IsDirty))
            {
                repartitionable.PartitionRequested = true;

                if (repartitionable.HasTransform)
                    repartitionable.InPosition = World.Get<TransformComponent>(entity).Cached.WorldPosition;
            }
            else
                partitionComponent.IsDirty = false;
        }

        [Query]
        private void RepartitionAll(ref Repartitionable repartitionable, ref PartitionComponent partitionComponent)
        {
            if (!repartitionable.PartitionRequested) return;

            repartitionable.IdInJob = idInJob++;
            AddToJob(partitionComponent, repartitionable.InPosition);
            repartitionable.PartitionRequested = false;

            if (repartitionable.IsNewEntity)
            {
                partitionComponent.IsDirty = true;
                repartitionable.IsNewEntity = false;
            }
        }

        private void AddToJob(PartitionComponent partitionComponent, Vector3 inPosition)
        {
            bucket.Add(partitionComponent.Bucket);
            isBehind.Add(partitionComponent.IsBehind);
            position.Add(inPosition);
        }
    }

    public struct Repartitionable
    {
        public int IdInJob;

        public Vector3 InPosition;
        public bool IsNewEntity;
        public bool HasTransform;
        public bool PartitionRequested;
    }
}
