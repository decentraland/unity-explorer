using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

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
