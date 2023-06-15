using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Common;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Components
{
    public struct GltfContainerComponent
    {
        public string Source => Promise.LoadingIntention.Name;

        public ColliderLayer VisibleMeshesCollisionMask;
        public ColliderLayer InvisibleMeshesCollisionMask;

        public AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> Promise;

        public EnumState<LoadingState> State;

        public GltfContainerComponent(ColliderLayer visibleMeshesCollisionMask, ColliderLayer invisibleMeshesCollisionMask, AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> promise)
        {
            VisibleMeshesCollisionMask = visibleMeshesCollisionMask;
            InvisibleMeshesCollisionMask = invisibleMeshesCollisionMask;
            Promise = promise;
            State = new EnumState<LoadingState>(LoadingState.Unknown);
        }
    }
}
