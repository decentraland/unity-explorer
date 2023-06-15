using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Components.Defaults;
using ECS.Unity.Transforms.Components;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.Unity.GLTFContainer.Asset.Components.GltfContainerAsset, ECS.Unity.GLTFContainer.Asset.Components.GetGltfContainerAssetIntention>;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Resolves the state of Gltf Container Loading.
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    public partial class LoadGltfContainerSystem : BaseUnityLoopSystem
    {
        internal LoadGltfContainerSystem(World world) : base(world) { }

        public override void BeforeUpdate(in float t)
        {
            ResetStateChangedQuery(World);
        }

        protected override void Update(float t)
        {
            StartLoadingQuery(World);
            ReconfigureGltfContainerQuery(World);
            FinalizeLoadingQuery(World);
        }

        [Query]
        private void ResetStateChanged(ref GltfContainerComponent component)
        {
            component.State.SetFramePassed();
        }

        [Query]
        [None(typeof(GltfContainerComponent))]
        private void StartLoading(in Entity entity, ref PBGltfContainer sdkComponent)
        {
            // It's not the best idea to pass Transform directly but we rely on cancellation source to cancel if the entity dies
            var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, new CancellationTokenSource()));
            var component = new GltfContainerComponent(sdkComponent.GetVisibleMeshesCollisionMask(), sdkComponent.GetInvisibleMeshesCollisionMask(), promise);
            component.State.Set(LoadingState.Loading);
            World.Add(entity, component);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeLoading(ref GltfContainerComponent component, ref TransformComponent transformComponent)
        {
            // Try consume removes the entity if the loading is finished
            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                // TODO error reporting
                if (!result.Succeeded)
                {
                    component.State.Set(LoadingState.FinishedWithError);
                    Debug.LogException(result.Exception);
                    return;
                }

                SetupColliders(ref component, result.Asset);

                // Reparent to the current transform
                result.Asset.Root.transform.SetParent(transformComponent.Transform);
                result.Asset.Root.SetActive(true);

                component.State.Set(LoadingState.Finished);
            }
        }

        // SDK Component was changed
        [Query]
        private void ReconfigureGltfContainer(ref GltfContainerComponent component, ref PBGltfContainer sdkComponent)
        {
            if (sdkComponent.IsDirty)
            {
                switch (component.State.Value)
                {
                    // The source is changed, should start downloading over again
                    case LoadingState.Unknown:
                        var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, new CancellationTokenSource()));
                        component.Promise = promise;
                        component.State.Set(LoadingState.Loading);
                        return;

                    // Clean-up is handled by ResetGltfContainerSystem so "InProgress" is not considered here
                    // Do nothing if finished with error
                    case LoadingState.Finished:
                        Assert.IsTrue(component.Promise.Result.HasValue);

                        // if promise was unsuccessful nothing to do
                        StreamableLoadingResult<GltfContainerAsset> result = component.Promise.Result.Value;

                        if (!result.Succeeded)
                            return;

                        ColliderLayer visibleCollisionMask = sdkComponent.GetVisibleMeshesCollisionMask();

                        if (visibleCollisionMask != component.VisibleMeshesCollisionMask)
                        {
                            component.VisibleMeshesCollisionMask = visibleCollisionMask;
                            SetupVisibleColliders(ref component, result.Asset);
                        }

                        ColliderLayer invisibleCollisionMask = sdkComponent.GetInvisibleMeshesCollisionMask();

                        if (invisibleCollisionMask != component.InvisibleMeshesCollisionMask)
                        {
                            component.InvisibleMeshesCollisionMask = invisibleCollisionMask;
                            SetupInvisibleColliders(ref component, result.Asset);
                        }

                        return;
                }
            }
        }
    }
}
