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
        private int idInJobCounter;

        private NativeList<byte> bucket = new (1024, Allocator.Persistent);
        private NativeList<bool> isBehind = new (1024, Allocator.Persistent);
        private NativeList<Vector3> position = new (1024, Allocator.Persistent);

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

        protected override void Update(float t)
        {
            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;

            RePartitionExistingSceneEntitiesQuery(World, samplingData.IsDirty, scenePosition);
            PartitionNewEntitiesQuery(World, scenePosition);

            if (idInJobCounter != 0)
            {
                Debug.Log("VVV running the job");
                handle = new PartitionJob
                {
                    FastPathSqrDistance = partitionSettings.FastPathSqrDistance,
                    SqrDistanceBuckets = sqrDistanceBuckets,
                    CameraPosition = samplingData.Position,
                    CameraForward = samplingData.Forward,
                    SceneBucket = scenePartition.Bucket,
                    SceneIsBehind = scenePartition.IsBehind,

                    Bucket = bucket,
                    IsBehind = isBehind,
                    Position = position,
                }.Schedule(idInJobCounter, 64);

                handle.Complete();
                ApplyJobResultsQuery(World);
                ClearJob();
            }
        }

        [Query]
        private void ApplyJobResults(ref Repartitionable repartitionable, ref PartitionComponent partitionComponent)
        {
            if (repartitionable.IdInJob < 0) return;

            partitionComponent.Bucket = bucket[repartitionable.IdInJob];
            partitionComponent.IsBehind = isBehind[repartitionable.IdInJob];

            partitionComponent.IsDirty = repartitionable.IsNewEntity
                                         || partitionComponent.Bucket != bucket[repartitionable.IdInJob]
                                         || partitionComponent.IsBehind != isBehind[repartitionable.IdInJob];
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        [None(typeof(PartitionComponent))]
        private void PartitionNewEntities(in Entity entity, [Data] Vector3 scenePosition)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();

            var repartitionable = new Repartitionable();

            Vector3 pos = World.TryGet(entity, out TransformComponent transformComponent) ? transformComponent.Cached.WorldPosition : scenePosition;
            AddToJob(ref repartitionable, partitionComponent, pos);
            repartitionable.IsNewEntity = true;

            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        private void RePartitionExistingSceneEntities(in Entity entity, ref Repartitionable repartitionable, ref PartitionComponent partitionComponent, [Data] bool playerPositionOrRotationChanged, [Data] Vector3 scenePosition)
        {
            bool hasTransform = World.TryGet(entity, out TransformComponent transformComponent) && World.Has<SDKTransform>(entity);

            if (playerPositionOrRotationChanged)
            {
                Vector3 pos = hasTransform ? transformComponent.Cached.WorldPosition : scenePosition;
                AddToJob(ref repartitionable, partitionComponent, pos);
            }
            else // re-partition if Entity transform changed
            {
                partitionComponent.IsDirty = false;
                repartitionable.IdInJob = -1;

                if (hasTransform && World.Get<SDKTransform>(entity).IsDirty)
                    AddToJob(ref repartitionable, partitionComponent, transformComponent.Cached.WorldPosition);
            }
        }

        private void AddToJob(ref Repartitionable repartitionable, PartitionComponent partitionComponent, Vector3 inPosition)
        {
            Debug.Log("VVV adding to job");

            repartitionable.IsNewEntity = false;
            repartitionable.IdInJob = idInJobCounter;

            bucket.Add(partitionComponent.Bucket);
            isBehind.Add(partitionComponent.IsBehind);
            position.Add(inPosition);

            idInJobCounter++;
        }

        private void ClearJob()
        {
            bucket.Clear();
            isBehind.Clear();
            position.Clear();

            idInJobCounter = 0;
        }
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

    public struct Repartitionable
    {
        public int IdInJob;
        public bool IsNewEntity;
    }
}
