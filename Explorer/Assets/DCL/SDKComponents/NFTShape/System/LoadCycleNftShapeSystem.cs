using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateNftShapeSystem))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly IURNSource urnSource;
        private readonly IMediaFactory mediaFactory;

        public LoadCycleNftShapeSystem(World world, IURNSource urnSource,
            IMediaFactory mediaFactory) : base(world)
        {
            this.urnSource = urnSource;
            this.mediaFactory = mediaFactory;
        }

        protected override void Update(float t)
        {
            StartQuery(World!);
            ResolveTypeQuery(World!);
            FinishAndApplyQuery(World!);
        }

        [Query]
        [None(typeof(NFTLoadingComponent))]
        private void Start(in Entity entity, in PBNftShape nftShape, in PartitionComponent partitionComponent)
        {
            var promise = NftTypePromise.Create(World!, new GetNFTTypeIntention(urnSource.UrlOrEmpty(nftShape.Urn!)), partitionComponent);
            World!.Add(entity, new NFTLoadingComponent(nftShape.Urn, promise));
        }

        [Query]
        private void ResolveType(ref NFTLoadingComponent nftLoadingComponent,
            ref NftShapeRendererComponent nftShapeRendererComponent,
            in PartitionComponent partitionComponent)
        {
            if (nftLoadingComponent.TypePromise.IsConsumed
                || !nftLoadingComponent.TypePromise.TryConsume(World!, out StreamableLoadingResult<NftTypeResult> result)) return;

            WebContentInfo.ContentType type = result.Asset.Type;
            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            switch (type)
            {
                case WebContentInfo.ContentType.Image or WebContentInfo.ContentType.KTX2:
                    nftLoadingComponent.ImagePromise = NftImagePromise.Create(World!, GetNFTImageIntention.Create(result.Asset.URL), partitionComponent);
                    break;
                case WebContentInfo.ContentType.Video:
                    // See https://github.com/decentraland/unity-explorer/issues/5611
                    var textureData = new TextureData(AnyTexture.FromVideoTextureData(mediaFactory.CreateVideoPlayback(result.Asset.URL)));
                    // The system won't add reference as it uses NoCache that has an empty implementation of AddReference
                    textureData.AddReference();

                    ApplyVideo(ref nftLoadingComponent, ref nftShapeRendererComponent, textureData);
                    break;
                default:
                    nftRenderer.NotifyFailed();
                    break;
            }
        }

        [Query]
        private void FinishAndApply(ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            ApplyImage(ref nftLoadingComponent, ref nftShapeRendererComponent);
        }

        private void ApplyImage(ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.ImagePromise is not { } promise) return;
            if (promise.IsConsumed || !promise.TryConsume(World!, out StreamableLoadingResult<TextureData> result)) return;

            // Need to reassign promise, otherwise it becomes outdated once it is consumed. It is a reference issue due to handling a copy of it
            nftLoadingComponent.ImagePromise = promise;

            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded || result.Asset == null || result.Asset.Asset == null)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            nftRenderer.Apply(result.Asset!);
        }

        private void ApplyVideo(
            ref NFTLoadingComponent nftLoadingComponent,
            ref NftShapeRendererComponent nftShapeRendererComponent,
            TextureData textureData)
        {
            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;
            AnyTexture anyTexture = textureData.Asset;

            nftRenderer.Apply(anyTexture.Texture);

            // No need to check if video texture really, but there is no other way to get the VideoTextureData..
            if (anyTexture.IsVideoTextureData(out VideoTextureData? videoTextureData))
                nftLoadingComponent.VideoPlayerEntity = World.Create(anyTexture,
                    new CustomMediaStream(MediaPlayerComponent.DEFAULT_VOLUME, true),
                    videoTextureData!.Value.Consumer,
                    videoTextureData.Value.MediaPlayer);
        }
    }
}
