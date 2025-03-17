using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
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
        internal PrepareGlobalAssetBundleLoadingParametersSystem(World world, URLDomain streamingAssetURL) : base(world, streamingAssetURL) { }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]

        // Provides a unique asset bundle manifest for each entity containing an asset bundle
        private new void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            // TODO hack, fix Asset Bundle Converter
            assetBundleIntention.Hash = assetBundleIntention.Manifest.FixCapitalization(assetBundleIntention.Hash);

            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state);
        }

    }
}
