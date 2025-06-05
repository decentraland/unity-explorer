using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.WebRequests;
using DCL.WebRequests.WebContentSizes;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.NFTShapes.DTOs;
using ECS.StreamableLoading.Textures;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTShapeSystem : LoadSystemBase<Texture2DData, GetNFTShapeIntention>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IWebContentSizes webContentSizes;
        private readonly bool ktxEnabled;

        public LoadNFTShapeSystem(World world, IStreamableCache<Texture2DData, GetNFTShapeIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache, IWebContentSizes webContentSizes,
            bool ktxEnabled)
            : base(
                world, cache, new DiskCacheOptions<Texture2DData, GetNFTShapeIntention>(diskCache, GetNFTShapeIntention.DiskHashCompute.INSTANCE, "nft")
            )
        {
            this.webRequestController = webRequestController;
            this.webContentSizes = webContentSizes;
            this.ktxEnabled = ktxEnabled;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTShapeIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            Uri imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);

            if (!ktxEnabled)
            {
                bool isOkSize = await webContentSizes.IsOkSizeAsync(imageUrl, ct);

                if (isOkSize == false)
                    return new StreamableLoadingResult<Texture2DData>(GetReportCategory(), new Exception("Image size is too big"));
            }

            // No need to check the size since we're using our converter so size will always be ok
            // texture request
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            IOwnedTexture2D? result = await webRequestController.GetTextureAsync(
                                                                     new CommonLoadingArguments(imageUrl, attempts: 1),
                                                                     new GetTextureArguments(TextureType.Albedo, true),
                                                                     GetReportData())
                                                                .CreateTextureAsync(GetNFTShapeIntention.WRAP_MODE, GetNFTShapeIntention.FILTER_MODE, ct);


            if (result == null)
                return new StreamableLoadingResult<Texture2DData>(
                    GetReportData(),
                    new Exception($"Error loading texture from url {intention.CommonArguments.URL}")
                );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
        }

        private async UniTask<Uri> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericGetRequest? infoRequest = webRequestController.GetAsync(commonArguments, GetReportData());
            NftInfoDto nft = await infoRequest.CreateFromJsonAsync<NftInfoDto>(WRJsonParser.Unity, ct, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
