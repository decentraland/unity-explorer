using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
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
        [None(typeof(LoadingInProgress), typeof(StreamableLoadingResult<AssetBundleData>))]

        // If loading is not started yet and there is no result
        private void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention)
        {
            // Remove not supported flags
            assetBundleIntention.RemovePermittedSource(AssetSource.ADDRESSABLE); // addressables are not implemented

            // If Hash is already provided just use it, otherwise resolve by the content provider
            if (assetBundleIntention.Hash == null)
            {
                if (!sceneData.TryGetHash(assetBundleIntention.Name, out assetBundleIntention.Hash))
                {
                    // TODO Errors reporting
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (new ArgumentException($"Asset Bundle {assetBundleIntention.Name} not found in the content")));

                    return;
                }

                // TODO Hack, kill me
                assetBundleIntention.Hash = assetBundleIntention.Hash.GetHashCode().ToString();
            }

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
                // If Hash is already provided just use it, otherwise resolve by the content provider
                if (!sceneData.AssetBundleManifest.Contains(assetBundleIntention.Hash))
                {
                    // TODO Errors reporting
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (new ArgumentException($"Asset Bundle {assetBundleIntention.Hash} not found in the manifest")));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                ca.URL = sceneData.AssetBundleManifest.GetAssetBundleURL(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = sceneData.AssetBundleManifest.ComputeHash(assetBundleIntention.Hash);
            }
        }

        private string GetStreamingAssetsUrl(string hash) =>
            $"{streamingAssetURL}{hash}";
    }
}
