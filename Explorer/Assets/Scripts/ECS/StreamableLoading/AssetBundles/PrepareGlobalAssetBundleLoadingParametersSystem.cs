using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Prepares Asset Bundle Parameters for loading Asset Bundle in the global world
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadGlobalAssetBundleSystem))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrepareGlobalAssetBundleLoadingParametersSystem : PrepareAssetBundleLoadingParametersSystemBase
    {
        public PrepareGlobalAssetBundleLoadingParametersSystem(World world, string streamingAssetURL) : base(world, streamingAssetURL) { }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]

        // Provides a unique asset bundle manifest for each entity containing an asset bundle
        private void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state,
            ref SceneAssetBundleManifest assetBundleManifest)
        {
            PrepareCommonArguments(assetBundleManifest, in entity, ref assetBundleIntention, ref state);
        }

        /// <summary>
        ///     Can't resolve hash for global asset bundles
        /// </summary>
        protected override bool TryResolveHash(ref GetAssetBundleIntention assetBundleIntention) =>
            false;
    }
}
