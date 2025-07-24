﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
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
            ManifestHelper manifestHelper = ManifestHelper.Create(sceneData.SceneEntityDefinition.assetBundleManifestVersion,
                sceneData.SceneEntityDefinition.id,
                sceneData.SceneEntityDefinition.hasSceneInPath);
            assetBundleIntention.ManifestHelper = manifestHelper;
            base.PrepareCommonArguments(in entity, ref assetBundleIntention, ref state);
        }

    }
}
