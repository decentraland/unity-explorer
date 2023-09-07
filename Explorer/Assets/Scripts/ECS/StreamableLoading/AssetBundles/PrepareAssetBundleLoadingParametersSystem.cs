using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Prepares Asset Bundle Parameters for loading Asset Bundle in the scene world
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrepareAssetBundleLoadingParametersSystem : PrepareAssetBundleLoadingParametersSystemBase
    {
        private readonly ISceneData sceneData;

        internal PrepareAssetBundleLoadingParametersSystem(World world, ISceneData sceneData, string streamingAssetURL) : base(world, streamingAssetURL)
        {
            this.sceneData = sceneData;
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
            PrepareCommonArguments(sceneData.AssetBundleManifest, in entity, ref assetBundleIntention, ref state);
        }

        protected override bool TryResolveHash(ref GetAssetBundleIntention assetBundleIntention) =>
            sceneData.TryGetHash(assetBundleIntention.Name, out assetBundleIntention.Hash);
    }
}
