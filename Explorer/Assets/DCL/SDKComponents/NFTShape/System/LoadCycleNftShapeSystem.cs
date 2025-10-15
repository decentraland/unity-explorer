using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
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
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateNftShapeSystem))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly IURNSource urnSource;
        private readonly ExtendedObjectPool<Texture2D> videoTexturePool;

        public LoadCycleNftShapeSystem(World world, IURNSource urnSource,
            ExtendedObjectPool<Texture2D> videoTexturePool) : base(world)
        {
            this.urnSource = urnSource;
            this.videoTexturePool = videoTexturePool;
        }

        protected override void Update(float t)
        {
            StartQuery(World!);
            ResolveTypeQuery(World!);
            ResolveNftShapeQuery(World!);
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
                    var texture2D = videoTexturePool.Get();
                    var data = new Texture2DData(texture2D, result.Asset.URL);
                    // See https://github.com/decentraland/unity-explorer/issues/5611
                    // Since caching is not used here, we need to add a reference to the data
                    // to prevent it from being cleaned up automatically
                    data.AddReference();

                    ResolveVideo(entity, ref nftShapeRendererComponent, data);
                    break;
                default:
                    nftRenderer.NotifyFailed();
                    break;
            }
        }

        [Query]
        private void ResolveNftShape(ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            ResolveImage(ref nftLoadingComponent, ref nftShapeRendererComponent);
        }

        private void ResolveImage(ref NFTLoadingComponent nftLoadingComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
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

        private void ResolveVideo(in Entity entity,
            ref NftShapeRendererComponent nftShapeRendererComponent,
            Texture2DData textureData)
        {
            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            nftRenderer.Apply(textureData.Asset);

            if (textureData.VideoURL != null)
                Play(entity, textureData);

            return;

            void Play(in Entity entity, Texture2DData textureData)
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
}
