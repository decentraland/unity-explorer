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

        private HashSet<int> entityToPartition = new (64);
        private NativeArray<byte> bucket = new (2, Allocator.TempJob);
        private NativeArray<bool> isBehind = new ();
        private NativeArray<Vector3> entityPosition = new ();
        private NativeArray<bool> isDirty = new ();

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
        private void RepartitionAll([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, ref Repartition repartition, ref PartitionComponent partitionComponent)
        {
            if (repartition.Processed) return;

            RePartition(cameraPosition, cameraForward, repartition.InPosition, partitionComponent);
            repartition.Processed = true;

            if (repartition.IsNewEntity)
            {
                partitionComponent.IsDirty = true;
                repartition.IsNewEntity = false;
            }
        }

        private void RePartition(Vector3 cameraTransform, Vector3 cameraForward, Vector3 entityPosition, PartitionComponent partitionComponent)
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
    }

    public struct Repartition
    {
        public Vector3 InPosition;
        public bool IsNewEntity;
        public bool HasTransform;
        public bool Processed;
    }
}
