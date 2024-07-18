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
    }
}
