using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;
using NftVideoPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTVideoIntention>;

namespace DCL.SDKComponents.NFTShape.Component
{
    public struct NFTLoadingComponent
    {
        public NftTypePromise TypePromise;
        public NftImagePromise ImagePromise;
        public NftVideoPromise VideoPromise;

        public readonly string OriginalUrn;

        public NFTLoadingComponent(string originalUrn, NftTypePromise typePromise)
        {
            this.OriginalUrn = originalUrn;
            TypePromise = typePromise;
            ImagePromise = default(NftImagePromise);
            VideoPromise = default(NftVideoPromise);
        }

        public readonly override string ToString() =>
            $"NFTLoadingComponent {{ promise: {TypePromise.Entity} {TypePromise.LoadingIntention.CommonArguments.URL} {OriginalUrn} }}";
    }
}
