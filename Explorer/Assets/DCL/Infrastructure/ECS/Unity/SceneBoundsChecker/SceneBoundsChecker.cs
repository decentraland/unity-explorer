using Arch.Core;
using Arch.System;
using ECS.LifeCycle;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.ECS.Unity.SceneBoundsChecker
{
    public partial class SceneBoundsChecker : ISceneIsCurrentListener
    {
        private readonly World sceneWorld;
        private readonly ISceneStateProvider sceneStateProvider;

        public SceneBoundsChecker(World sceneWorld,
            ISceneStateProvider sceneStateProvider)
        {
            this.sceneWorld = sceneWorld;
            this.sceneStateProvider = sceneStateProvider;
        }

        void ISceneIsCurrentListener.OnSceneIsCurrentChanged(bool value)
        {
            if (sceneStateProvider.State.Value() == SceneState.Disposed)
                return;

            if (value)
            {
                EnablePrimitiveCollidersQuery(sceneWorld);
                EnableGltfCollidersQuery(sceneWorld);
            }
            else
            {
                DisablePrimitiveCollidersQuery(sceneWorld);
                DisableGltfCollidersQuery(sceneWorld);
            }
        }

        [Query]
        private void DisableGltfColliders(GltfContainerComponent component) =>
            ForceActiveBySceneBounds(component, false);

        [Query]
        private void DisablePrimitiveColliders(PrimitiveColliderComponent collider) =>
            collider.SDKCollider.ForceActiveBySceneBounds(false);

        [Query]
        private void EnableGltfColliders(GltfContainerComponent component) =>
            ForceActiveBySceneBounds(component, true);

        [Query]
        private void EnablePrimitiveColliders(PrimitiveColliderComponent collider) =>
            collider.SDKCollider.ForceActiveBySceneBounds(true);

        private void ForceActiveBySceneBounds(GltfContainerComponent component,
            bool value)
        {
            if (!component.Promise.TryGetResult(sceneWorld, out var result))
                return;

            GltfContainerAsset? asset = result.Asset;

            if (asset == null)
                return;

            ForceActiveBySceneBounds(asset.InvisibleColliders, value);

            if (asset.DecodedVisibleSDKColliders != null)
                ForceActiveBySceneBounds(asset.DecodedVisibleSDKColliders, value);
        }

        private static void ForceActiveBySceneBounds(List<SDKCollider> colliders, bool value)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                SDKCollider sdkCollider = colliders[i];
                sdkCollider.ForceActiveBySceneBounds(value);
                colliders[i] = sdkCollider;
            }
        }
    }
}
