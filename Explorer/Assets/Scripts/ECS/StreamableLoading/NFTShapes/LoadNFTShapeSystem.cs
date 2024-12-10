using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
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

        public LoadNFTShapeSystem(World world, IStreamableCache<Texture2DData, GetNFTShapeIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTShapeIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);

            // texture request
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                new CommonLoadingArguments(URLAddress.FromString(imageUrl), attempts: 1),
                new GetTextureArguments(TextureType.Albedo),
                new GetTextureWebRequest.CreateTextureOp(GetNFTShapeIntention.WRAP_MODE, GetNFTShapeIntention.FILTER_MODE),
                ct,
                GetReportData()
            );

            if (result == null)
                return new StreamableLoadingResult<Texture2DData>(
                    GetReportData(),
                    new Exception($"Error loading texture from url {intention.CommonArguments.URL}")
                );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
        }

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> infoRequest = webRequestController.GetAsync(commonArguments, ct, GetReportData());
            var nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
