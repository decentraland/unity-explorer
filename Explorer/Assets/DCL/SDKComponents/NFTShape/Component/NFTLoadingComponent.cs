using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Component
{
    public struct NFTLoadingComponent
    {
        public Promise Promise;

        public NFTLoadingComponent(Promise promise)
        {
            Promise = promise;
        }

        public readonly override string ToString() =>
            $"NFTLoadingComponent {{ promise: {Promise.Entity.Entity} {Promise.LoadingIntention.CommonArguments.URL} }}";
    }
}
