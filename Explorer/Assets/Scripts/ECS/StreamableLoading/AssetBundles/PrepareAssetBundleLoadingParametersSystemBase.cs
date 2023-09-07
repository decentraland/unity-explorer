using Arch.Core;
using Arch.System;
using AssetManagement;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    public abstract class PrepareAssetBundleLoadingParametersSystemBase : BaseUnityLoopSystem
    {
        private readonly string streamingAssetURL;

        protected PrepareAssetBundleLoadingParametersSystemBase(World world, string streamingAssetURL) : base(world)
        {
            this.streamingAssetURL = streamingAssetURL;
        }

        protected abstract bool TryResolveHash(ref GetAssetBundleIntention assetBundleIntention);

        protected void PrepareCommonArguments([Data] SceneAssetBundleManifest assetBundleManifest, in Entity entity,
            ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
        {
            if (state.Value != StreamableLoadingState.Status.NotStarted) return;

            // Remove not supported flags
            assetBundleIntention.RemovePermittedSource(AssetSource.ADDRESSABLE); // addressables are not implemented

            // If Hash is already provided just use it, otherwise resolve by the content provider
            if (assetBundleIntention.Hash == null)
            {
                if (!TryResolveHash(ref assetBundleIntention))
                {
                    // Add the failure to the entity
                    var exception = new ArgumentException($"Asset Bundle {assetBundleIntention.Name} not found in the content");
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>(CreateException(exception)));

                    return;
                }

                assetBundleIntention.Hash += PlatformUtils.GetPlatform();
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
                if (!assetBundleManifest.Contains(assetBundleIntention.Hash))
                {
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (CreateException(new ArgumentException($"Asset Bundle {assetBundleIntention.Hash} {assetBundleIntention.Name} not found in the manifest"))));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                ca.URL = assetBundleManifest.GetAssetBundleURL(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = assetBundleManifest.ComputeHash(assetBundleIntention.Hash);
            }
        }

        private string GetStreamingAssetsUrl(string hash) =>
            $"{streamingAssetURL}{hash}";
    }
}
