using Arch.Core;
using Arch.System;
using DCL.ECSComponents;
using ECS.LifeCycle;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.Unity.SceneBoundsChecker
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
        private void DisableGltfColliders(ref GltfContainerComponent component) =>
            ForceActiveBySceneBounds(ref component, false);

        [Query]
        private void DisablePrimitiveColliders(ref PrimitiveColliderComponent component) =>
            ForceActiveBySceneBounds(ref component, false);

        [Query]
        private void EnableGltfColliders(ref GltfContainerComponent component) =>
            ForceActiveBySceneBounds(ref component, true);

        [Query]
        private void EnablePrimitiveColliders(ref PrimitiveColliderComponent component) =>
            ForceActiveBySceneBounds(ref component, true);

        private static void ForceActiveBySceneBounds(
            ref PrimitiveColliderComponent component, bool value) =>
            component.SDKCollider.ForceActiveBySceneBounds(value);

        private void ForceActiveBySceneBounds(ref GltfContainerComponent component,
            bool value)
        {
            if (component.State != LoadingState.Finished)
                return;

            var asset = component.Promise.Result?.Asset;

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
                SDKCollider collider = colliders[i];
                collider.ForceActiveBySceneBounds(value);
                colliders[i] = collider;
            }
        }
    }
}
