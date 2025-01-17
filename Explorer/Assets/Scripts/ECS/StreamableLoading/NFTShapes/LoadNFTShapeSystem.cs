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
using Plugins.TexturesFuse.TexturesServerWrap;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTShapeSystem : PartialDownloadSystemBase<Texture2DData, GetNFTShapeIntention>
    {
        private readonly IWebRequestController webRequestController;
        private readonly ITexturesFuse texturesFuse;

        public LoadNFTShapeSystem(
            World world,
            IStreamableCache<Texture2DData, GetNFTShapeIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            ITexturesFuse texturesFuse) : base(world, cache, webRequestController, buffersPool)
        {
            this.webRequestController = webRequestController;
            this.texturesFuse = texturesFuse;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTShapeIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            var arguments = intention.CommonArguments;
            arguments.URL = URLAddress.FromString(imageUrl);
            intention.CommonArguments = arguments;
            return await base.FlowInternalAsync(intention, state, partition, ct);
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> ProcessCompletedData(byte[] completeData, GetNFTShapeIntention intention, IPartitionComponent partition,  CancellationToken ct)
        {
            EnumResult<IOwnedTexture2D,NativeMethods.ImageResult> textureFromBytesAsync = await texturesFuse.TextureFromBytesAsync(completeData, TextureType.Albedo, ct);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(textureFromBytesAsync.Value));
        }

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> infoRequest = webRequestController.GetAsync(commonArguments, ct, GetReportData());
            var nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
