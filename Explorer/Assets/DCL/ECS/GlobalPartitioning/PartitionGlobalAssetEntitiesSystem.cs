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
        private readonly IPartitionSettings partitionSettings;
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;

        internal PartitionGlobalAssetEntitiesSystem(World world, IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings settings, IReadOnlyCameraSamplingData cameraSamplingData) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            partitionSettings = settings;
            samplingData = cameraSamplingData;
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
        private void PartitionNewEntity([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward, in Entity entity, ref CharacterTransform transformComponent)
        {
            PartitionComponent partitionComponent = partitionComponentPool.Get();
            RePartition(cameraPosition, cameraForward, transformComponent.Transform.position, ref partitionComponent);
            partitionComponent.IsDirty = true;
            World.Add(entity, partitionComponent);
        }

        [Query]
        [Any(typeof(PBAvatarShape), typeof(Profile))]
        [None(typeof(PlayerComponent))]
        private void RePartitionExistingEntity([Data] Vector3 cameraPosition, [Data] Vector3 cameraForward,
            ref CharacterTransform transformComponent, ref PartitionComponent partitionComponent)
        {
            RePartition(cameraPosition, cameraForward, transformComponent.Transform.position, ref partitionComponent);
        }

        private void RePartition(Vector3 cameraTransform, Vector3 cameraForward, Vector3 entityPosition, ref PartitionComponent partitionComponent)
        {
            // TODO pure math logic can be jobified for much better performance

            byte bucket = partitionComponent.Bucket;
            bool isBehind = partitionComponent.IsBehind;

            // check if fast path should be used
            Vector3 vectorToCamera = entityPosition - cameraTransform;
            float sqrDistance = Vector3.SqrMagnitude(vectorToCamera);

            PartitionAssetEntitiesSystem.ResolvePartitionFromDistance(partitionSettings, cameraForward, partitionComponent, sqrDistance, vectorToCamera);

            partitionComponent.IsDirty = bucket != partitionComponent.Bucket || isBehind != partitionComponent.IsBehind;
        }
    }
}
