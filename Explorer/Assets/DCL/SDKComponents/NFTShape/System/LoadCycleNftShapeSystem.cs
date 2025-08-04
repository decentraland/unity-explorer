using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Renderer;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

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
            FinishAndApplyQuery(World!);
        }

        [Query]
        [None(typeof(NFTLoadingComponent))]
        private void Start(in Entity entity, in PBNftShape nftShape, in PartitionComponent partitionComponent)
        {
            var promise = Promise.Create(World!, new GetNFTShapeIntention(nftShape.Urn!, urnSource), partitionComponent);
            World!.Add(entity, new NFTLoadingComponent(promise));
        }

        [Query]
        private void FinishAndApply(Entity entity, ref NFTLoadingComponent nftLoadingComponent, in NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.Promise.IsConsumed || !nftLoadingComponent.Promise.TryConsume(World!, out StreamableLoadingResult<Texture2DData> result)) return;

            INftShapeRenderer nftRenderer = nftShapeRendererComponent.PoolableComponent;

            if (!result.Succeeded)
            {
                nftRenderer.NotifyFailed();
                return;
            }

            nftRenderer.Apply(result.Asset!);
            nftRenderer.SetTextureScale(Vector2.one);

            if (result.Asset?.VideoURL != null) InitializeNftVideo(entity, result.Asset, nftRenderer);
        }

        private void InitializeNftVideo(Entity entity, Texture2DData textureData, INftShapeRenderer nftRenderer)
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
