﻿using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using DCL.WebRequests.WebContentSizes;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.NFTShapes.DTOs;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.NFTShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNFTShapeSystem : LoadSystemBase<Texture2D, GetNFTShapeIntention>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IWebContentSizes webContentSizes;

        public LoadNFTShapeSystem(World world, IStreamableCache<Texture2D, GetNFTShapeIntention> cache, IWebRequestController webRequestController, MutexSync mutexSync, IWebContentSizes webContentSizes) : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
            this.webContentSizes = webContentSizes;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternalAsync(GetNFTShapeIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string imageUrl = await ImageUrlAsync(intention.CommonArguments, ct);
            bool isOkSize = await webContentSizes.IsOkSizeAsync(imageUrl, ct);

            if (isOkSize == false)
                return new StreamableLoadingResult<Texture2D>(new Exception("Image size is too big"));

            //texture request
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            GetTextureWebRequest request = await webRequestController.GetTextureAsync(
                new CommonLoadingArguments(URLAddress.FromString(imageUrl), attempts: 1),
                new GetTextureArguments(false),
                ct,
                reportCategory: GetReportCategory()
            );

            return new StreamableLoadingResult<Texture2D>(request.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear)!);
        }

        private async UniTask<string> ImageUrlAsync(CommonArguments commonArguments, CancellationToken ct)
        {
            var infoRequest = await webRequestController.GetAsync(commonArguments, ct, GetReportCategory());
            var nft = await infoRequest.CreateFromJson<NftInfoDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);
            return nft.ImageUrl();
        }
    }
}
