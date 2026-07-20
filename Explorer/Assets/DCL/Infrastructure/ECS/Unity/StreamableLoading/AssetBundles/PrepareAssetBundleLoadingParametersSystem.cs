using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Groups;
using ECS.StreamableLoading.Common.Components;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Prepares Asset Bundle Parameters for loading Asset Bundle in the scene world
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrepareAssetBundleLoadingParametersSystem : PrepareAssetBundleLoadingParametersSystemBase
    {
        internal PrepareAssetBundleLoadingParametersSystem(World world, URLDomain streamingAssetURL, URLDomain assetBundlesURL) : base(world, streamingAssetURL, assetBundlesURL) { }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]
        // If loading is not started yet and there is no result
        private new void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state);
        }

    }
}
