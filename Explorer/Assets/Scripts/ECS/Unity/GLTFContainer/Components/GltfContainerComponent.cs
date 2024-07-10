using DCL.ECSComponents;
using ECS.StreamableLoading.Common;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Components
{
    public struct GltfContainerComponent
    {
        private const string FAULTY_HASH = "FAULTY_HASH";
        
        public string Hash;
        public string Name;

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
            Hash = Promise.LoadingIntention.Hash;
            Name = Promise.LoadingIntention.Name;
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
            Hash = FAULTY_HASH;
            Name = name;
            Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL;
        }

        public void UpdatePromise(AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> promise)
        {
            Promise = promise;
            Hash = Promise.LoadingIntention.Hash;
            Name = Promise.LoadingIntention.Name;
            State = LoadingState.Loading;
        }
    }
}
