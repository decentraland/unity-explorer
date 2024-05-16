using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    public abstract class PrepareAssetBundleLoadingParametersSystemBase : BaseUnityLoopSystem
    {
        private static readonly string[] COMMON_SHADERS = { "dcl/scene_ignore_windows", "dcl/scene_ignore_mac" };

        private readonly URLDomain streamingAssetURL;

        protected PrepareAssetBundleLoadingParametersSystemBase(World world, URLDomain streamingAssetURL) : base(world)
        {
            this.streamingAssetURL = streamingAssetURL;
        }

        protected abstract bool TryResolveHash(ref GetAssetBundleIntention assetBundleIntention);

        protected void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
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
                ca.URL = GetStreamingAssetsUrl(assetBundleIntention.Hash, assetBundleIntention.CommonArguments.CustomEmbeddedSubDirectory);
                assetBundleIntention.CommonArguments = ca;
                return;
            }

            // Second priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.WEB))
            {
                if (assetBundleIntention.Manifest == null)
                {
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (CreateException(new ArgumentException($"Manifest must be provided to load {assetBundleIntention.Name} from `WEB` source"))));

                    return;
                }

                if (!assetBundleIntention.Manifest.Contains(assetBundleIntention.Hash))
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
                ca.URL = assetBundleIntention.Manifest.GetAssetBundleURL(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = assetBundleIntention.Manifest.ComputeHash(assetBundleIntention.Hash);
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));
    }
}
