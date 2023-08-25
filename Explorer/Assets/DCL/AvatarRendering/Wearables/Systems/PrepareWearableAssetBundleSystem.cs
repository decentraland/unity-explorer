using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using Utility;

namespace DCL.AvatarRendering.Wearables.Systems
{
    /// <summary>
    ///     Prepares the Wearable Asset Bundle Parameters
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadWearableAssetBundleSystem))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrepareWearableAssetBundleLoadingParametersSystem : BaseUnityLoopSystem
    {
        private readonly string STREAMING_ASSETS_URL;

        internal PrepareWearableAssetBundleLoadingParametersSystem(World world, string streamingAssetURL) : base(world)
        {
            STREAMING_ASSETS_URL = streamingAssetURL;
        }

        protected override void Update(float t)
        {
            PrepareCommonArgumentsQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<AssetBundleData>))]

        // If loading is not started yet and there is no result
        private void PrepareCommonArguments(in Entity entity, ref GetWearableAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            if (state.Value != StreamableLoadingState.Status.NotStarted) return;

            // Remove not supported flags
            assetBundleIntention.RemovePermittedSource(AssetSource.ADDRESSABLE); // addressables are not implemented

            // First priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.EMBEDDED))
            {
                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = 1;
                ca.CurrentSource = AssetSource.EMBEDDED;
                ca.URL = GetStreamingAssetsUrl(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = assetBundleIntention.AssetBundleManifest.ComputeHash(assetBundleIntention.Hash);
                return;
            }

            // Second priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.WEB))
            {
                // If Hash is already provided just use it, otherwise resolve by the content provider
                if (!assetBundleIntention.AssetBundleManifest.Contains(assetBundleIntention.Hash))
                {
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (CreateException(new ArgumentException($"Asset Bundle {assetBundleIntention.Hash} not found in the manifest"))));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                ca.URL = assetBundleIntention.AssetBundleManifest.GetAssetBundleURL(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = assetBundleIntention.AssetBundleManifest.ComputeHash(assetBundleIntention.Hash);
            }
        }

        private string GetStreamingAssetsUrl(string hash) =>
            $"{STREAMING_ASSETS_URL}{hash}";
    }
}
