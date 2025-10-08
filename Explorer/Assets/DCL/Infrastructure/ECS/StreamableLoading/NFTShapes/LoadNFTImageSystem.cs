using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.Textures;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTImageSystem : LoadSystemBase<Texture2DData, GetNFTImageIntention>
    {
        private readonly IWebRequestController webRequestController;

        public LoadNFTImageSystem(World world, IStreamableCache<Texture2DData, GetNFTImageIntention> cache,
            IWebRequestController webRequestController)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetNFTImageIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                new CommonLoadingArguments(URLAddress.FromString(intention.CommonArguments.URL), attempts: 1),
                new GetTextureArguments(TextureType.Albedo, true),
                new GetTextureWebRequest.CreateTextureOp(TextureWrapMode.Clamp, FilterMode.Bilinear),
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
    }
}
