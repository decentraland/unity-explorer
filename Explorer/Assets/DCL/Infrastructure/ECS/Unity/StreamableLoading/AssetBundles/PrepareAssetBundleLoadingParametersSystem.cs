using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Groups;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Prepares Asset Bundle Parameters for loading Asset Bundle in the scene world
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrepareAssetBundleLoadingParametersSystem : PrepareAssetBundleLoadingParametersSystemBase
    {
        private readonly bool localSceneDevelopment;

        internal PrepareAssetBundleLoadingParametersSystem(World world, URLDomain streamingAssetURL, URLDomain assetBundlesURL, bool localSceneDevelopment, bool entityScopedBundleUrls)
            : base(world, streamingAssetURL, assetBundlesURL, entityScopedBundleUrls)
        {
            this.localSceneDevelopment = localSceneDevelopment;
        }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]
        // If loading is not started yet and there is no result
        private void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            // Local-scene dev bundle ids are path-derived and keep the same hash across edits, so cached entries would go stale forever
            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state, ignoreCacheHash: localSceneDevelopment);
        }

    }
}
