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
using Unity.Collections;
using Unity.Jobs;
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

        protected override void Update(float t)
        {
            RequestRepartitionForAllEntitiesQuery(World, samplingData.IsDirty);

            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            MarkNewEntitiesForPartitionQuery(World, scenePosition);

            Vector3 cameraPosition = samplingData.Position;
            Vector3 cameraForward = samplingData.Forward;

            RepartitionAllQuery(World, cameraPosition, cameraForward);

            NativeArray<int> sqrDistanceBuckets = new NativeArray<int>(partitionSettings.SqrDistanceBuckets.Count, Allocator.Persistent);
            for (var i = 0; i < partitionSettings.SqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];

            Dictionary<Entity, int> entityToJobId = new Dictionary<Entity, int>();

            NativeArray<byte> Bucket = new NativeArray<byte>(entitiesToRepartition.Count, Allocator.TempJob);
            NativeArray<bool> IsBehind = new NativeArray<bool>(entitiesToRepartition.Count, Allocator.TempJob);
            NativeArray<Vector3> Position = new NativeArray<Vector3>(entitiesToRepartition.Count, Allocator.TempJob);

            int j = 0;
            foreach (var jobPreparationData in entitiesToRepartition)
            {
                entityToJobId[jobPreparationData.Entity] = j;

                Bucket[j] = jobPreparationData.Bucket;
                IsBehind[j] = jobPreparationData.IsBehind;
                Position[j] = jobPreparationData.Position;

                j++;
            }

         // HashSet<int> entityToPartition = new (64);
         // NativeArray<byte> bucket = new (2, Allocator.TempJob);
         // NativeArray<bool> isBehind = new ();
         // NativeArray<Vector3> entityPosition = new ();
         // NativeArray<bool> isDirty = new ();


        }

        private readonly HashSet<RepartitionJobData> entitiesToRepartition = new (64);

        public struct RepartitionJobData
        {
            public Entity Entity;
            public byte Bucket;
            public bool IsBehind;
            public Vector3 Position;
        }

        public struct MyJob : IJobParallelFor
        {
            public int FastPathSqrDistance;
            public NativeArray<int> SqrDistanceBuckets;

            public Vector3 CameraPosition;
            public Vector3 CameraForward;

            public byte SceneBucket;
            public bool SceneIsBehind;

            [ReadOnly] public NativeArray<RepartitionJobData> RepartitionData;
            public NativeArray<byte> Bucket;
            public NativeArray<bool> IsBehind;
            public NativeArray<Vector3> Position;

            public void Execute(int index)
            {
                Bucket[index] = RepartitionData[index].Bucket;
                IsBehind[index] = RepartitionData[index].IsBehind;

                Vector3 vectorToCamera = RepartitionData[index].Position - CameraPosition;
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

                // IsDirty[index] = Bucket[index] != RepartitionData[index].Bucket || IsBehind[index] != RepartitionData[index].IsBehind;
            }
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))] // PbMaterial is attached to the renderer and can contain textures
        [None(typeof(Repartition), typeof(PartitionComponent))]
        private void MarkNewEntitiesForPartition(in Entity entity, [Data] Vector3 scenePosition)
        {
            bool hasTransform = World.Has<TransformComponent>(entity);

            Vector3 position = hasTransform
                ? World.Get<TransformComponent>(entity).Cached.WorldPosition
                : scenePosition;

            PartitionComponent partitionComponent = partitionComponentPool.Get();

            World.Add(entity,
                partitionComponent,
                new Repartition { InPosition = position, IsNewEntity = true, HasTransform = hasTransform, Processed = false });
        }

        [Query]
        private void RequestRepartitionForAllEntities(in Entity entity, ref Repartition repartition, ref PartitionComponent partitionComponent, [Data] bool playerTransformChanged)
        {
            if (playerTransformChanged || (World.Has<SDKTransform>(entity) && World.Get<SDKTransform>(entity).IsDirty))
            {
                repartition.Processed = false;

                if (repartition.HasTransform)
                    repartition.InPosition = World.Get<TransformComponent>(entity).Cached.WorldPosition;
            }
            else
                partitionComponent.IsDirty = false;
        }

        [Query]
        private void RepartitionAll(in Entity entity, ref Repartition repartition, ref PartitionComponent partitionComponent, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward)
        {
            if (repartition.Processed) return;

            // RePartition(cameraPosition, cameraForward, repartition.InPosition, partitionComponent);

            entitiesToRepartition.Add(new RepartitionJobData
            {
                Entity = entity,
                Bucket = partitionComponent.Bucket,
                IsBehind = partitionComponent.IsBehind,
                Position = repartition.InPosition
            });

            repartition.Processed = true;

            if (repartition.IsNewEntity)
            {
                partitionComponent.IsDirty = true;
                repartition.IsNewEntity = false;
            }
        }




        private void RePartition(Vector3 cameraPosition, Vector3 cameraForward, Vector3 entityPosition, PartitionComponent partitionComponent)
        {
            // TODO pure math logic can be jobified for much better performance

            byte bucket = partitionComponent.Bucket;
            bool isBehind = partitionComponent.IsBehind;

            // check if fast path should be used
            Vector3 vectorToCamera = entityPosition - cameraPosition;
            float sqrDistance = Vector3.SqrMagnitude(vectorToCamera);

            if (sqrDistance > partitionSettings.FastPathSqrDistance)
            {
                // just inherit Scene's values
                partitionComponent.Bucket = scenePartition.Bucket;
                partitionComponent.IsBehind = scenePartition.IsBehind;
            }
            else
            {
                // Find the bucket
                byte bucketIndex;
                for (bucketIndex = 0; bucketIndex < partitionSettings.SqrDistanceBuckets.Count; bucketIndex++)
                    if (sqrDistance < partitionSettings.SqrDistanceBuckets[bucketIndex])
                        break;

                partitionComponent.Bucket = bucketIndex;
                partitionComponent.IsBehind = Vector3.Dot(cameraForward, vectorToCamera) < 0;
            }

            partitionComponent.IsDirty = bucket != partitionComponent.Bucket || isBehind != partitionComponent.IsBehind;
        }
    }

    public struct Repartition
    {
        public Vector3 InPosition;
        public bool IsNewEntity;
        public bool HasTransform;
        public bool Processed;
    }
}
