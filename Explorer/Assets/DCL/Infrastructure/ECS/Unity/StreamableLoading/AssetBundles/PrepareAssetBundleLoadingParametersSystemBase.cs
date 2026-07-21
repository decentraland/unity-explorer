using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    public abstract class PrepareAssetBundleLoadingParametersSystemBase : BaseUnityLoopSystem
    {
        private static readonly string[] COMMON_SHADERS =
        {
            "dcl/scene_ignore_windows", "dcl/scene_ignore_mac",
            "dcl/universal render pipeline/lit_ignore_windows",
            "dcl/universal render pipeline/lit_ignore_mac",
        };

        private readonly URLDomain streamingAssetURL;
        private readonly URLDomain assetBundlesURL;

        protected PrepareAssetBundleLoadingParametersSystemBase(World world, URLDomain streamingAssetURL, URLDomain assetBundlesURL) : base(world)
        {
            this.streamingAssetURL = streamingAssetURL;
            this.assetBundlesURL = assetBundlesURL;
        }

        protected void PrepareCommonArguments(in Entity entity, ref GetAssetBundleIntention assetBundleIntention, ref StreamableLoadingState state)
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
                ca.URL = GetStreamingAssetsUrl(assetBundleIntention.Hash, assetBundleIntention.CommonArguments.CustomEmbeddedSubDirectory);
                assetBundleIntention.CommonArguments = ca;

                return;
            }

            // Second priority
            if (EnumUtils.HasFlag(assetBundleIntention.CommonArguments.PermittedSources, AssetSource.WEB))
            {
                if (assetBundleIntention.AssetBundleManifest.assetBundleManifestRequestFailed)
                {
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (GetReportCategory(), CreateException(new ArgumentException($"Manifest version must be provided to load {assetBundleIntention.Name} from `WEB` source"))));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;

                // Hash was already translated to the canonical CDN file name (digest and Qm casing) at intention creation via GetCdnRequestHash.
                ca.URL = GetAssetBundleURL(assetBundleIntention.AssetBundleManifest, assetBundleIntention.Hash, assetBundleIntention.ParentEntityID);
                assetBundleIntention.CommonArguments = ca;

                assetBundleIntention.cacheHash = assetBundleIntention.AssetBundleManifest.ComputeCacheHash(assetBundleIntention.Hash);
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));

        private URLAddress GetAssetBundleURL(AssetBundleManifestVersion manifest, string hash, string sceneID)
        {
            string version = manifest.GetAssetBundleManifestVersion();

            // Canonical-assets bundles live under the assets/ prefix (no entity segment) — requesting it directly
            // skips the edge rewrite. Entity-scoped bundles (wearables/emotes, pre-v49 scenes) only resolve through
            // the entity path, so they keep the legacy shapes.
            if (manifest.HasCanonicalAssets())
                return assetBundlesURL.Append(new URLPath($"{version}/assets/{hash}"));

            if (manifest.HasHashInPath())
                return assetBundlesURL.Append(new URLPath($"{version}/{sceneID}/{hash}"));

            return assetBundlesURL.Append(new URLPath($"{version}/{hash}"));
        }

    }
}
