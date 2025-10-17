using Arch.Core;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Component
{
    public struct NFTLoadingComponent
    {
        public Promise Promise;

        public Entity VideoPlayerEntity;

        public NFTLoadingComponent(Promise promise)
        {
            Promise = promise;
            VideoPlayerEntity = Entity.Null;
        }

        public readonly override string ToString() =>
            $"NFTLoadingComponent {{ promise: {Promise.Entity} {Promise.LoadingIntention.CommonArguments.URL}, VideoPlayerEntity: {VideoPlayerEntity} }}";
    }
}
