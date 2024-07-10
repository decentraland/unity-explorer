using DCL.ECSComponents;
using ECS.StreamableLoading.Common;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Components
{
    public struct GltfContainerComponent
    {
        private const string FAULTY_HASH = "FAULTY_HASH";
        
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

        public static GltfContainerComponent CreateFaulty(string name)
        {
            GltfContainerComponent component = new GltfContainerComponent();
            component.SetFaulty(name);
            return component;
        }
            

        public void SetFaulty(string name)
        {
            State = LoadingState.FinishedWithError;
            Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.CreateFinalized(
                new GetGltfContainerAssetIntention(name, FAULTY_HASH, null),
                null);
        }

    }
}
