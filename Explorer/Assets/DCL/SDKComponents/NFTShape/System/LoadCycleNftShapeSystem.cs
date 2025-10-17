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
using ECS.Unity.Textures.Components;
using UnityEngine;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateNftShapeSystem))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly IURNSource urnSource;

        public LoadCycleNftShapeSystem(World world, IURNSource urnSource) : base(world)
        {
            this.urnSource = urnSource;
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
        private void ResolveType(in Entity entity,
            ref NFTLoadingComponent nftLoadingComponent,
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
                    nftLoadingComponent.ImagePromise = NftImagePromise.Create(World!, new GetNFTImageIntention(result.Asset.URL), partitionComponent);
                    break;
                case WebContentInfo.ContentType.Video:
                    // TODO
                    //var texture2D = videoTexturePool.Get();
                    //var data = new Texture2DData(texture2D, result.Asset.URL);
                    // See https://github.com/decentraland/unity-explorer/issues/5611
                    // Since caching is not used here, we need to add a reference to the data
                    // to prevent it from being cleaned up automatically
                    //data.AddReference();

                    //ResolveVideo(entity, ref nftShapeRendererComponent, data);
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
            if (promise.IsConsumed || !promise.TryConsume(World!, out StreamableLoadingResult<Texture2DData> result)) return;

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
            if (nftLoadingComponent.Promise.IsConsumed || !nftLoadingComponent.Promise.TryConsume(World!, out StreamableLoadingResult<TextureData> result)) return;

            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded || result.Asset == null || result.Asset.Asset == null)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            AnyTexture anyTexture = result.Asset.Asset;

            nftRenderer.Apply(anyTexture.Texture);

            if (anyTexture.IsVideoTextureData(out VideoTextureData? videoTextureData))
                nftLoadingComponent.VideoPlayerEntity = World.Create(result.Asset!, new CustomMediaStream(MediaPlayerComponent.DEFAULT_VOLUME, true), videoTextureData!.Value.Consumer, videoTextureData.Value.MediaPlayer);
        }
    }
}
