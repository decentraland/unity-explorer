using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Prepares Asset Bundle Parameters
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    public partial class PrepareAssetBundleLoadingParametersSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly string streamingAssetURL;

        internal PrepareAssetBundleLoadingParametersSystem(World world, ISceneData sceneData, string streamingAssetURL) : base(world)
        {
            this.sceneData = sceneData;
            this.streamingAssetURL = streamingAssetURL;
        }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(LoadingInProgress), typeof(StreamableLoadingResult<AssetBundle>))]

        // If loading is not started yet and there is no result
        private void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention)
        {
            // First priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.EMBEDDED))
            {
                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = 1;
                ca.CurrentSource = AssetSource.EMBEDDED;
                ca.URL = GetStreamingAssetsUrl(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                return;
            }

            // Second priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.WEB))
            {
                // Ignore root manifest for dependencies, they are not listed there
                if (!sceneData.AssetBundleManifest.TryGetAssetBundleURL(assetBundleIntention.Hash, !assetBundleIntention.IsDependency, out string url))
                {
                    // TODO Errors reporting
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundle>
                        (new ArgumentException($"Asset Bundle {assetBundleIntention.Hash} not found in the manifest")));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                ca.URL = url;
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = sceneData.AssetBundleManifest.ComputeHash(assetBundleIntention.Hash);
            }
        }

        private string GetStreamingAssetsUrl(string hash) =>
            $"{streamingAssetURL}{hash}";
    }
}
