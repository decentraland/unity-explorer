using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Time;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.SceneBoundsChecker
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(GltfContainerGroup))]
    public partial class CheckColliderBoundsSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Colliders according to the collision matrix affect Character Controller (player) only.
        ///     We should take care only about asset which are close enough.
        ///     No assets farther than this bucket will be checked
        /// </summary>
        internal const byte BUCKET_THRESHOLD = 1;

        private readonly IPartitionComponent scenePartition;
        private readonly ParcelMathHelper.SceneGeometry sceneGeometry;
        private readonly IPhysicsTickProvider physicsTickEntity;
        private Bounds auxiliaryBounds = new Bounds();

        /// <summary>
        ///     Throttle scheduling between fixed updates
        /// </summary>
        private int lastFixedFrameChecked;

        internal CheckColliderBoundsSystem(World world, IPartitionComponent scenePartition, ParcelMathHelper.SceneGeometry sceneGeometry, IPhysicsTickProvider physicsTickEntity) : base(world)
        {
            this.scenePartition = scenePartition;
            this.sceneGeometry = sceneGeometry;
            this.physicsTickEntity = physicsTickEntity;
        }

        protected override void Update(float t)
        {
            int tick = physicsTickEntity.Tick;
            if (tick == lastFixedFrameChecked) return;

            lastFixedFrameChecked = tick;

            CheckPrimitives();
            CheckGltfAssetQuery(World);
        }

        private void CheckPrimitives()
        {
            // We don't partition primitives separately so for them there will be no PartitionComponent assigned
            if (scenePartition.Bucket > BUCKET_THRESHOLD) return;

            CheckPrimitiveQuery(World);
        }

        [Query]
        private void CheckPrimitive(ref PrimitiveColliderComponent primitiveCollider)
        {
            Collider collider = primitiveCollider.Collider;

            if (!collider || !primitiveCollider.SDKCollider.IsActiveByEntity) return;

            if (!primitiveCollider.SDKCollider.HasMoved())
                return;

            // We use an auxiliary bounds object as Unity physics may take at least an extra frame to
            // actually update the min-max of the collider bounds, leaving the collider disabled in the end...
            auxiliaryBounds.center = collider.transform.position;
            auxiliaryBounds.extents = collider.bounds.extents;
            primitiveCollider.SDKCollider.ForceActiveBySceneBounds(auxiliaryBounds.max.y <= sceneGeometry.Height
                                                                   && sceneGeometry.CircumscribedPlanes.Intersects(auxiliaryBounds));
        }

        [Query]
        private void CheckGltfAsset(ref GltfContainerComponent component, ref PartitionComponent partitionComponent)
        {
            if (component.State != LoadingState.Finished) return;

            if (scenePartition.Bucket > BUCKET_THRESHOLD && partitionComponent.Bucket > BUCKET_THRESHOLD)
                return;

            // can't be null when state is Finished
            GltfContainerAsset asset = component.Promise.Result.Value.Asset;

            // Process all colliders

            // Visible meshes colliders are created on demand
            if (asset.DecodedVisibleSDKColliders != null)
                ProcessColliders(asset.DecodedVisibleSDKColliders);

            ProcessColliders(asset.InvisibleColliders);
            return;

            void ProcessColliders(List<SDKCollider> colliders)
            {
                for (var i = 0; i < colliders.Count; i++)
                {
                    SDKCollider sdkCollider = colliders[i];

                    // if sdk collider is disabled by entity it's not needed to process it
                    // it will be processed in the frame it's enabled again
                    if (!sdkCollider.IsActiveByEntity)
                        continue;

                    if (!sdkCollider.HasMoved())
                        continue;

                    // We use an auxiliary bounds object as Unity physics may take at least an extra frame to
                    // actually update the min-max of the collider bounds, leaving the collider disabled in the end...
                    auxiliaryBounds.center = sdkCollider.Collider.transform.position;
                    auxiliaryBounds.extents = sdkCollider.Collider.bounds.extents;

                    // While the collider remains inactive, the bounds will continue to be zero, causing incorrect calculations.
                    // Therefore, it is necessary to force the collider to be activated at least once
                    sdkCollider.ForceActiveBySceneBounds(auxiliaryBounds.extents == Vector3.zero
                                                         || (auxiliaryBounds.max.y <= sceneGeometry.Height && sceneGeometry.CircumscribedPlanes.Intersects(auxiliaryBounds)));

                    // write the structure back
                    colliders[i] = sdkCollider;
                }
            }
        }
    }
}
