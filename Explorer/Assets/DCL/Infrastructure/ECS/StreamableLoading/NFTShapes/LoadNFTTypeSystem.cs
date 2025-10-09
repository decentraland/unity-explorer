using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.NFTShapes.DTOs;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTTypeSystem : LoadSystemBase<NftTypeResult, GetNFTTypeIntention>
    {
        private const long MAX_PREVIEW_SIZE = 8388608;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly bool ktxEnabled;

        public LoadNFTTypeSystem(World world, IStreamableCache<NftTypeResult, GetNFTTypeIntention> cache,
            IWebRequestController webRequestController, bool ktxEnabled, IDecentralandUrlsSource urlsSource)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.ktxEnabled = ktxEnabled;
        }

        protected override async UniTask<StreamableLoadingResult<NftTypeResult>> FlowInternalAsync(GetNFTTypeIntention intention,
            StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            string convertUrl = ktxEnabled ? string.Format(urlsSource.Url(DecentralandUrl.MediaConverter), Uri.EscapeDataString(imageUrl)) : imageUrl;
            var contentInfo = await WebContentInfo.FetchAsync(convertUrl, ct);

            if (!ktxEnabled && contentInfo is { Type: WebContentInfo.ContentType.Image, SizeInBytes: > MAX_PREVIEW_SIZE })
                return new StreamableLoadingResult<NftTypeResult>(GetReportCategory(), new Exception("Image size is too big"));

            return new StreamableLoadingResult<NftTypeResult>(new NftTypeResult(contentInfo.Type, URLAddress.FromString(convertUrl)));
        }

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> infoRequest = webRequestController.GetAsync(commonArguments, ct, GetReportData());
            var nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
