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
        private readonly ISceneData sceneData;
        private readonly bool localSceneDevelopment;

        internal PrepareAssetBundleLoadingParametersSystem(World world, ISceneData sceneData, URLDomain streamingAssetURL, URLDomain assetBundlesURL, bool localSceneDevelopment) : base(world, streamingAssetURL, assetBundlesURL)
        {
            this.sceneData = sceneData;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]
        // If loading is not started yet and there is no result
        private new void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            assetBundleIntention.AssetBundleManifestVersion = sceneData.SceneEntityDefinition.assetBundleManifestVersion;
            assetBundleIntention.ParentEntityID = sceneData.SceneEntityDefinition.id;

            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state);

            // Local-scene dev bundles are addressed by path-derived ids that keep the same hash
            // across edits, so a disk-cache entry could serve them stale forever; dropping the
            // cache hash makes the request bypass Unity Caching.
            if (localSceneDevelopment)
                assetBundleIntention.cacheHash = null;
        }

    }
}
