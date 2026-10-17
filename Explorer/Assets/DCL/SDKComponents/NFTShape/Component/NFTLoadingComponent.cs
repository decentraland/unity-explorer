using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using Arch.Core;

namespace DCL.SDKComponents.NFTShape.Component
{
    public struct NFTLoadingComponent
    {
        public NftTypePromise TypePromise;
        public NftImagePromise? ImagePromise;
        public Entity VideoPlayerEntity;

        public readonly string OriginalUrn;

        public NFTLoadingComponent(string originalUrn, NftTypePromise typePromise)
        {
            this.OriginalUrn = originalUrn;
            TypePromise = typePromise;
            ImagePromise = null;
            VideoPlayerEntity = Entity.Null;
        }

        public readonly override string ToString() =>
            $"NFTLoadingComponent {{ promise: {TypePromise.Entity} {TypePromise.LoadingIntention.CommonArguments.URL}, OriginalUrn: {OriginalUrn}, VideoPlayerEntity: {VideoPlayerEntity} }}";
    }
}
