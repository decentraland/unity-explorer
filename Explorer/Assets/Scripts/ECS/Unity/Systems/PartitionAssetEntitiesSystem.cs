using Arch.Buffer;
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
using UnityEngine;

namespace ECS.Unity.Systems
{
    public struct PartitionRequest { public Vector3 InPosition; }

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

        private readonly CommandBuffer buffer = new ();

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
            if (!samplingData.IsDirty)
                ResetDirtyQuery(World);

            RequestPartitionForEntitiesQuery(World, samplingData.IsDirty, World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition);

            ProcessPartitioningQuery(World,
                cameraposition: samplingData.Position,
                cameraforward: samplingData.Forward);

            buffer.Playback(World);
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]// PbMaterial is attached to the renderer and can contain textures
        private void ResetDirty(ref PartitionComponent partitionComponent)
        {
            partitionComponent.IsDirty = false;
        }

        [Query]
        [Any(typeof(PBNftShape), typeof(PBGltfContainer), typeof(PBMaterial), typeof(PBAvatarShape), typeof(PBAudioSource), typeof(PBAudioStream), typeof(PBUiBackground), typeof(PBRaycast))]
        private void RequestPartitionForEntities(Entity entity, [Data] bool playerTransformHasChanged, [Data] Vector3 scenePosition)
        {
            if (!World.Has<PartitionComponent>(entity)
                || playerTransformHasChanged
                || (World.TryGet<SDKTransform>(entity, out var sdkTransform) && sdkTransform.IsDirty))
            {
                World.Add(entity, new PartitionRequest
                {
                    InPosition = World.TryGet(entity, out TransformComponent transformComponent)
                        ? transformComponent.Cached.WorldPosition
                        : scenePosition,
                });
            }
        }

        [Query]
        private void ProcessPartitioning(Entity entity, ref PartitionRequest request, [Data] Vector3 cameraPosition, [Data] Vector3 cameraForward)
        {
            if(World.TryGet<PartitionComponent>(entity, out var partitionComponent))
            {
                RePartition(cameraPosition, cameraForward, request.InPosition, ref partitionComponent);
            }
            else
            {
                partitionComponent = partitionComponentPool.Get();
                buffer.Add(entity, partitionComponent);

                RePartition(cameraPosition, cameraForward, request.InPosition, ref partitionComponent);
                partitionComponent.IsDirty = true;
            }

            buffer.Remove<PartitionRequest>(entity);
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
            else
            {
                // Find the bucket
                byte bucketIndex;

                for (bucketIndex = 0; bucketIndex < partitionSettings.SqrDistanceBuckets.Count; bucketIndex++)
                    if (sqrDistance < partitionSettings.SqrDistanceBuckets[bucketIndex])
                        break;

                partitionComponent.Bucket = bucketIndex;
                partitionComponent.IsBehind = Vector3.Dot(cameraForward, vectorToCamera) < 0; // cheap as sqrMagnitude
            }

            partitionComponent.IsDirty = bucket != partitionComponent.Bucket || isBehind != partitionComponent.IsBehind;
        }
    }
}
