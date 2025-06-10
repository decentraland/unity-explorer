using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.NFTShapes.DTOs;
using ECS.StreamableLoading.Textures;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTShapeSystem : LoadSystemBase<Texture2DData, GetNFTShapeIntention>
    {
        private const long MAX_PREVIEW_SIZE = 8388608;

        private readonly IWebRequestController webRequestController;
        private readonly ExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly bool ktxEnabled;

        public LoadNFTShapeSystem(World world, IStreamableCache<Texture2DData, GetNFTShapeIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache, bool ktxEnabled,
            ExtendedObjectPool<Texture2D> videoTexturePool, IDecentralandUrlsSource urlsSource)
            : base(world, cache, new DiskCacheOptions<Texture2DData, GetNFTShapeIntention>(diskCache, GetNFTShapeIntention.DiskHashCompute.INSTANCE, "nft"))
        {
            this.webRequestController = webRequestController;
            this.videoTexturePool = videoTexturePool;
            this.urlsSource = urlsSource;
            this.ktxEnabled = ktxEnabled;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTShapeIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            Uri imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            Uri convertUrl = GetTextureWebRequest.GetEffectiveUrl(urlsSource, imageUrl, ktxEnabled);
            WebContentInfo contentInfo = await WebContentInfo.FetchAsync(webRequestController, convertUrl, GetReportData(), ct);

            if (!ktxEnabled && contentInfo is { Type: WebContentInfo.ContentType.Image, SizeInBytes: > MAX_PREVIEW_SIZE })
                return new StreamableLoadingResult<Texture2DData>(GetReportCategory(), new Exception("Image size is too big"));

            return contentInfo.Type switch
                   {
                       WebContentInfo.ContentType.Image or WebContentInfo.ContentType.KTX2 => await HandleImageAsync(imageUrl, ct),
                       WebContentInfo.ContentType.Video => HandleVideo(convertUrl),
                       _ => throw new NotSupportedException("Could not handle content type " + contentInfo.Type + " for url " + convertUrl)
                   };
        }

        private async UniTask<StreamableLoadingResult<Texture2DData>> HandleImageAsync(Uri url, CancellationToken ct)
        {
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                                                        new CommonLoadingArguments(url, attempts: 1),
                new GetTextureArguments(TextureType.Albedo, true),
                GetReportData())
                    .CreateTextureAsync(GetNFTShapeIntention.WRAP_MODE, GetNFTShapeIntention.FILTER_MODE, ct);


            if (result == null)
                return new StreamableLoadingResult<Texture2DData>(
                    GetReportData(),
                    new Exception($"Error loading texture from url {url}")
                );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
        }

        private StreamableLoadingResult<Texture2DData> HandleVideo(Uri url)
        {
            var texture2D = videoTexturePool.Get();
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture2D, url));
        }

        private async UniTask<Uri> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericGetRequest? infoRequest = webRequestController.GetAsync(commonArguments, GetReportData());
            NftInfoDto nft = await infoRequest.CreateFromJsonAsync<NftInfoDto>(WRJsonParser.Unity, ct, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
