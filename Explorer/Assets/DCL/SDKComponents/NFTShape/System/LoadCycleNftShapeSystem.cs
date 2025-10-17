using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Renderer;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

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
        private void FinishAndApply(ref NFTLoadingComponent nftLoadingComponent, in NftShapeRendererComponent nftShapeRendererComponent)
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
