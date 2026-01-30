using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.Unity.SceneBoundsChecker
{
    /// <summary>
    ///     Activates or deactivates all colliders in the scene based on whether the scene is current.
    ///     Replaces the more complex CheckColliderBoundsSystem that performed per-frame bounds checking.
    ///     Since only the player uses colliders for physics, we only need colliders active in the current scene.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(GltfContainerGroup))]
    public partial class SceneCollidersActivationSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly ISceneStateProvider sceneStateProvider;

        internal SceneCollidersActivationSystem(World world, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            // No per-frame work needed - activation is handled by OnSceneIsCurrentChanged
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (sceneStateProvider.State.Value() == SceneState.Disposed)
                return;

            if (isCurrent)
            {
                ActivatePrimitiveCollidersQuery(World);
                ActivateGltfCollidersQuery(World);
            }
            else
            {
                DeactivatePrimitiveCollidersQuery(World);
                DeactivateGltfCollidersQuery(World);
            }
        }

        [Query]
        private void ActivatePrimitiveColliders(ref PrimitiveColliderComponent primitiveCollider)
        {
            primitiveCollider.SDKCollider.ForceActiveBySceneBounds(true);
        }

        [Query]
        private void DeactivatePrimitiveColliders(ref PrimitiveColliderComponent primitiveCollider)
        {
            primitiveCollider.SDKCollider.ForceActiveBySceneBounds(false);
        }

        [Query]
        private void ActivateGltfColliders(ref GltfContainerComponent component)
        {
            if (component.State != LoadingState.Finished)
                return;

            GltfContainerAsset asset = component.Promise.Result.Value.Asset;

            SetCollidersActiveBySceneBounds(asset.InvisibleColliders, true);

            if (asset.DecodedVisibleSDKColliders != null)
                SetCollidersActiveBySceneBounds(asset.DecodedVisibleSDKColliders, true);
        }

        [Query]
        private void DeactivateGltfColliders(ref GltfContainerComponent component)
        {
            if (component.State != LoadingState.Finished)
                return;

            GltfContainerAsset asset = component.Promise.Result.Value.Asset;

            SetCollidersActiveBySceneBounds(asset.InvisibleColliders, false);

            if (asset.DecodedVisibleSDKColliders != null)
                SetCollidersActiveBySceneBounds(asset.DecodedVisibleSDKColliders, false);
        }

        private static void SetCollidersActiveBySceneBounds(List<SDKCollider> colliders, bool active)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                SDKCollider sdkCollider = colliders[i];
                sdkCollider.ForceActiveBySceneBounds(active);
                colliders[i] = sdkCollider;
            }
        }
    }
}
