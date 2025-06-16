using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using UnityEngine;
using Utility.Arch;
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
            if (!nftLoadingComponent.Promise.IsConsumed && nftLoadingComponent.Promise.TryConsume(World!, out StreamableLoadingResult<Texture2DData> result))
            {
                if (result.Succeeded)
                {
                    nftShapeRendererComponent.PoolableComponent.Apply(result.Asset!);

                    if (result.Asset?.VideoURL != null)
                    {
                        var vtc = new VideoTextureConsumer(result.Asset);
                        var texture2D = vtc.Texture.Asset;
                        texture2D.Reinitialize(1, 1);
                        texture2D.SetPixel(0, 0, Color.clear);
                        texture2D.Apply();

                        if (World.TryGet<PBVideoPlayer>(entity, out var videoPlayer))
                        {
                            videoPlayer!.Src = result.Asset.VideoURL.OriginalString;
                            videoPlayer.IsDirty = true;

                            World.Add(entity, vtc);
                        }
                        else
                        {
                            var pbVideo = new PBVideoPlayer
                            {
                                Src = result.Asset.VideoURL.OriginalString,
                                Playing = true,
                                Loop = true,
                            };

                            World.Add(entity, pbVideo, vtc);
                        }
                    }
                }
                else { nftShapeRendererComponent.PoolableComponent.NotifyFailed(); }
            }
        }
    }
}
