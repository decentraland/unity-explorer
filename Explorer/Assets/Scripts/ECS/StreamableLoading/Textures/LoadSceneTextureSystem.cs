using System;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadSceneTextureSystem : LoadTextureSystem
    {
        internal LoadSceneTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache) : base(world, cache, webRequestController, diskCache)
        {
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> GetTextureAsync(GetTextureIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            // First we try to get the AB texture. It will already be compressed and cached by the AB system
            var assetBundleResult = await TryAssetBundleDownload(intention, partition, ct);
            if (assetBundleResult.Succeeded)
                return assetBundleResult;

            // Fallback to regular texture download
            // Needed for external textures
            ReportHub.Log(GetReportCategory(), $"Texture not found in  scene AssetBundle. Downloading from URL: {intention.CommonArguments.URL.Value}");
            var result = await TryTextureDownload(intention, ct);

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }


        private async UniTask<StreamableLoadingResult<Texture2DData>> TryAssetBundleDownload(GetTextureIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(intention.FileHash))
            {
                string hash = intention.FileHash + PlatformUtils.GetCurrentPlatform();
                var assetBundlePromise
                    = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(typeof(Texture2D), hash), partition);
                try
                {
                    assetBundlePromise = await assetBundlePromise.ToUniTaskAsync(World, cancellationToken: ct);
                    if (assetBundlePromise.TryGetResult(World, out var depResult) && depResult.Succeeded)
                        return new StreamableLoadingResult<Texture2DData>(new Texture2DData(depResult.Asset!.GetMainAsset<Texture2D>().EnsureNotNull()));
                }
                catch (OperationCanceledException)
                {
                    assetBundlePromise.ForgetLoading(World);
                }
            }

            return default;
        }
    }
}