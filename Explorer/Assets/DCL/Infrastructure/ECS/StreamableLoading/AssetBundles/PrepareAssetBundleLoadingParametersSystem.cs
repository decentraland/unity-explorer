using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Groups;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
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

        internal PrepareAssetBundleLoadingParametersSystem(World world, ISceneData sceneData, URLDomain streamingAssetURL, URLDomain assetBundlesURL) : base(world, streamingAssetURL, assetBundlesURL)
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
        private new void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            assetBundleIntention.AssetBundleManifestVersion = sceneData.SceneEntityDefinition.assetBundleManifestVersion;
            assetBundleIntention.ParentEntityID = sceneData.SceneEntityDefinition.id;

            // In Bundle-mode ISS the shared bundle holds every listed asset, so redirect per-asset requests
            // to the bundle URL. Descriptor-mode and non-ISS scenes resolve their per-asset URLs unchanged.
            // No-op until LoadSceneSystemLogicBase resolves the descriptor (cache miss returns false).
            if (ISSDescriptorCache.INSTANCE.TryGet(GetISSDescriptor.For(sceneData.SceneEntityDefinition), out ISSDescriptor descriptor)
                && descriptor.IsBundleAsset(assetBundleIntention.Hash!))
                assetBundleIntention.Hash = GetAssetBundleIntention.BuildInitialSceneStateURL(assetBundleIntention.ParentEntityID);

            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state);
        }

    }
}
