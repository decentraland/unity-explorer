using System;
using DCL.ECSComponents;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Components
{
    public struct GltfContainerComponent
    {
        public string Hash => Promise.LoadingIntention.Hash;
        public string Name => Promise.LoadingIntention.Name;

        public ColliderLayer VisibleMeshesCollisionMask;
        public ColliderLayer InvisibleMeshesCollisionMask;

        public AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> Promise;

        public LoadingState State;

        public GltfContainerComponent(ColliderLayer visibleMeshesCollisionMask, ColliderLayer invisibleMeshesCollisionMask, AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> promise)
        {
            VisibleMeshesCollisionMask = visibleMeshesCollisionMask;
            InvisibleMeshesCollisionMask = invisibleMeshesCollisionMask;
            Promise = promise;
            State = LoadingState.Unknown;
        }

        public static GltfContainerComponent CreateFaulty(Exception exception)
        {
            GltfContainerComponent component = new GltfContainerComponent();
            component.SetFaulty(exception);
            return component;
        }

        public void SetFaulty(Exception exception)
        {
            State = LoadingState.FinishedWithError;
            Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.CreateFinalized(
                default,
                new StreamableLoadingResult<GltfContainerAsset>(exception));
        }

    }
}
