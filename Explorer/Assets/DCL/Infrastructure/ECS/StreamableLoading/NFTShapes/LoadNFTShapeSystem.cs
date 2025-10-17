using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SDKComponents.MediaStream;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common;
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
    public partial class LoadNFTShapeSystem : LoadSystemBase<TextureData, GetNFTShapeIntention>
    {
        private const long MAX_PREVIEW_SIZE = 8388608;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IMediaFactory mediaFactory;
        private readonly bool ktxEnabled;

        public LoadNFTShapeSystem(World world, IStreamableCache<TextureData, GetNFTShapeIntention> cache, IWebRequestController webRequestController, IDiskCache<TextureData> diskCache, bool ktxEnabled,
            IMediaFactory mediaFactory, IDecentralandUrlsSource urlsSource)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.mediaFactory = mediaFactory;
            this.ktxEnabled = ktxEnabled;
        }

        protected override async UniTask<StreamableLoadingResult<TextureData>> FlowInternalAsync(GetNFTShapeIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            string convertUrl = ktxEnabled ? string.Format(urlsSource.Url(DecentralandUrl.MediaConverter), Uri.EscapeDataString(imageUrl)) : imageUrl;
            var contentInfo = await WebContentInfo.FetchAsync(convertUrl, ct);

            if (!ktxEnabled && contentInfo is { Type: WebContentInfo.ContentType.Image, SizeInBytes: > MAX_PREVIEW_SIZE })
                return new StreamableLoadingResult<TextureData>(GetReportCategory(), new Exception("Image size is too big"));

            return contentInfo.Type switch
                   {
                       WebContentInfo.ContentType.Image or WebContentInfo.ContentType.KTX2 => await HandleImageAsync(imageUrl, partition, ct),
                       WebContentInfo.ContentType.Video => HandleVideo(convertUrl),
                       _ => throw new NotSupportedException("Could not handle content type " + contentInfo.Type + " for url " + convertUrl)
                   };
        }

        private async UniTask<StreamableLoadingResult<TextureData>> HandleImageAsync(string url, IPartitionComponent partition, CancellationToken ct)
        {
            // To prevent caching NFT Videos reuse the existing textures cache which is by design for Plain Images only

            var getTexture = new GetTextureIntention(url, GetNFTShapeIntention.WRAP_MODE, GetNFTShapeIntention.FILTER_MODE, TextureType.Albedo, nameof(LoadNFTShapeSystem), 1);

            var promise = AssetPromise<TextureData, GetTextureIntention>.Create(World, getTexture, partition);

            promise = await promise.ToUniTaskAsync(World, cancellationToken: ct);

            return promise.Result!.Value;
        }

        private StreamableLoadingResult<TextureData> HandleVideo(string url) =>
            new (new TextureData(AnyTexture.FromVideoTextureData(mediaFactory.CreateVideoPlayback(url))));

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> infoRequest = webRequestController.GetAsync(commonArguments, ct, GetReportData());
            var nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
