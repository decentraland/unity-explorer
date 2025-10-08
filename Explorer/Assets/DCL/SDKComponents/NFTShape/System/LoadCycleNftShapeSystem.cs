using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.DTOs;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using UnityEngine;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.DTOs.NftTypeDto, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;
using NftVideoPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTVideoIntention>;

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
            ResolveVideoQuery(World!);
        }

        [Query]
        [None(typeof(NFTLoadingComponent))]
        private void Start(in Entity entity, in PBNftShape nftShape, in PartitionComponent partitionComponent)
        {
            var promise = NftTypePromise.Create(World!, new GetNFTTypeIntention(urnSource.UrlOrEmpty(nftShape.Urn!)), partitionComponent);
            World!.Add(entity, new NFTLoadingComponent(nftShape.Urn, promise), partitionComponent);
        }

        [Query]
        private void ResolveType(ref NFTLoadingComponent nftLoadingComponent,
            ref NftShapeRendererComponent nftShapeRendererComponent,
            in PartitionComponent partitionComponent)
        {
            if (nftLoadingComponent.TypePromise.IsConsumed
                || !nftLoadingComponent.TypePromise.TryConsume(World!, out StreamableLoadingResult<NftTypeDto> result)) return;

            WebContentInfo.ContentType type = result.Asset.Type;
            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded
                || (type != WebContentInfo.ContentType.Image && type != WebContentInfo.ContentType.Video))
            {
                nftRenderer.NotifyFailed();
                return;
            }

            // We create different promises because the load of images and videos must be propagated into different systems due to caching issues
            switch (type)
            {
                case WebContentInfo.ContentType.Image when nftLoadingComponent.ImagePromise == null:
                    nftLoadingComponent.ImagePromise = NftImagePromise.Create(World!, new GetNFTImageIntention(result.Asset.URL), partitionComponent); break;
                case WebContentInfo.ContentType.Video when nftLoadingComponent.VideoPromise == null:
                    nftLoadingComponent.VideoPromise = NftVideoPromise.Create(World!, new GetNFTVideoIntention(result.Asset.URL), partitionComponent); break;
            }
        }

        [Query]
        private void ResolveImage(ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.ImagePromise == null) return;

            NftImagePromise promise = nftLoadingComponent.ImagePromise.Value;

            if (promise.IsConsumed || !promise.TryConsume(World!, out StreamableLoadingResult<Texture2DData> result)) return;

            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded || result.Asset == null || result.Asset.Asset == null)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            nftRenderer.Apply(result.Asset!);
        }

        [Query]
        private void ResolveVideo(Entity entity, ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.VideoPromise == null) return;

            NftVideoPromise promise = nftLoadingComponent.VideoPromise.Value;

            if (promise.IsConsumed || !promise.TryConsume(World!, out StreamableLoadingResult<Texture2DData> result)) return;

            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded || result.Asset == null || result.Asset.Asset == null)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            nftRenderer.Apply(result.Asset!);
            InitializeNftVideo(entity, result.Asset);
        }

        private void InitializeNftVideo(Entity entity, Texture2DData textureData)
        {
            var vtc = new VideoTextureConsumer(textureData);
            var texture2D = vtc.Texture.Asset;
            texture2D.Reinitialize(1, 1);
            texture2D.SetPixel(0, 0, Color.clear);
            texture2D.Apply();

            if (World.TryGet<PBVideoPlayer>(entity, out var videoPlayer))
            {
                videoPlayer!.Src = textureData.VideoURL;
                videoPlayer.IsDirty = true;

                World.Add(entity, vtc);
            }
            else
            {
                var pbVideo = new PBVideoPlayer
                {
                    Src = textureData.VideoURL,
                    Playing = true,
                    Loop = true,
                };

                World.Add(entity, pbVideo, vtc);
            }

            vtc.IsDirty = true;
        }
    }
}
