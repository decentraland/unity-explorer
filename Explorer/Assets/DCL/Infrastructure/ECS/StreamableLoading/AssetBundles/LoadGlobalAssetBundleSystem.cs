﻿using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadGlobalAssetBundleSystem : LoadAssetBundleSystem
    {
        internal LoadGlobalAssetBundleSystem(World world, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache, IWebRequestController webRequestController, AssetBundleLoadingMutex loadingMutex) : base(world, cache, webRequestController, loadingMutex) { }
    }
}
