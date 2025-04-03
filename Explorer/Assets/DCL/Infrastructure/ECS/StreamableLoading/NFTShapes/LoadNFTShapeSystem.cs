using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using DCL.WebRequests.WebContentSizes;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.NFTShapes.DTOs;
using ECS.StreamableLoading.Textures;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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

        public LoadNFTShapeSystem(World world, IStreamableCache<Texture2DData, GetNFTShapeIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache, IWebContentSizes webContentSizes)
            : base(
                world, cache, new DiskCacheOptions<Texture2DData, GetNFTShapeIntention>(diskCache, GetNFTShapeIntention.DiskHashCompute.INSTANCE, "nft")
            )
        {
            this.webRequestController = webRequestController;
            this.webContentSizes = webContentSizes;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTShapeIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            bool isOkSize = await webContentSizes.IsOkSizeAsync(imageUrl, ct);

            if (isOkSize == false)
                return new StreamableLoadingResult<Texture2DData>(GetReportCategory(), new Exception("Image size is too big"));

            // texture request
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            IOwnedTexture2D? result = await webRequestController.GetTextureAsync(
                                                                     new CommonLoadingArguments(URLAddress.FromString(imageUrl), attempts: 1),
                                                                     new GetTextureArguments(TextureType.Albedo),
                                                                     GetReportData())
                                                                .CreateTextureAsync(GetNFTShapeIntention.WRAP_MODE, GetNFTShapeIntention.FILTER_MODE, ct);

            if (result == null)
                return new StreamableLoadingResult<Texture2DData>(
                    GetReportData(),
                    new Exception($"Error loading texture from url {intention.CommonArguments.URL}")
                );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
        }

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericGetRequest? infoRequest = webRequestController.GetAsync(commonArguments, GetReportData());
            NftInfoDto nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, ct, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
