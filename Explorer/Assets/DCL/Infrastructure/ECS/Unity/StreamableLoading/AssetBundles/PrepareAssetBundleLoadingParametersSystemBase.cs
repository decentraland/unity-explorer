using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using DCL.Platforms;
using UnityEngine;
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
                if (assetBundleIntention.AssetBundleManifestVersion == null || assetBundleIntention.AssetBundleManifestVersion.assetBundleManifestRequestFailed)
                {
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (GetReportCategory(), CreateException(new ArgumentException($"Manifest version must be provided to load {assetBundleIntention.Name} from `WEB` source"))));

                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                assetBundleIntention.Hash = assetBundleIntention.AssetBundleManifestVersion.CheckCasing(assetBundleIntention.Hash);
                ca.URL = GetAssetBundleURL(assetBundleIntention.AssetBundleManifestVersion.HasHashInPath(), assetBundleIntention.Hash, assetBundleIntention.ParentEntityID, assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestVersion());
                assetBundleIntention.CommonArguments = ca;

                // v49+ hashes carry the deps digest inside the file name (<hash>_<depsDigest>_<platform>), so
                // (version + hash) is unique per dependency closure. Dispatch on whether the manifest carries the
                // deps map (only scene manifests do), not on whether this hash happens to carry a digest — an AB
                // with a legacy 2-part name inside a mapped manifest would otherwise be keyed on buildDate,
                // preventing cache sharing across CDN republishes. Wearables/emotes never carry the map and keep
                // buildDate keying: their bundles are republished in place and cannot be reused across builds.
                assetBundleIntention.cacheHash = assetBundleIntention.AssetBundleManifestVersion.HasDepsDigests()
                    ? ComputeHashV49(assetBundleIntention.Hash,
                        assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestVersion())
                    : ComputeHashLegacy(assetBundleIntention.Hash,
                        assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestBuildDate());
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));

        public static unsafe Hash128 ComputeHashLegacy(string hash, string buildDate)
        {
            // Byte-identical to the pre-v49 cache key so existing Unity-AB-cache entries keep hitting after upgrade.
            // The lack of a delimiter is a known theoretical collision risk (e.g. buildDate ending in 'X' vs. hash
            // starting with 'X') — accepted here, will be addressed when v49 adoption lets us retire this path.
            Span<char> hashBuilder = stackalloc char[buildDate.Length + hash.Length];
            buildDate.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[buildDate.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        public static unsafe Hash128 ComputeHashV49(string hash, string version)
        {
            // The per-file deps digest embedded in the hash replaces the buildDate sledgehammer that was previously
            // used to invalidate the cache whenever a dependency might have changed. Keying on (version + hash) lets
            // the cache stay shareable across CDN republishes when the dependency closure is unchanged. The delimiter
            // prevents boundary collisions between version and hash.
            ReadOnlySpan<char> hashSpan = hash.AsSpan();
            ReadOnlySpan<char> versionSpan = version.AsSpan();

            Span<char> builder = stackalloc char[versionSpan.Length + 1 + hashSpan.Length];
            versionSpan.CopyTo(builder);
            builder[versionSpan.Length] = '|';
            hashSpan.CopyTo(builder[(versionSpan.Length + 1)..]);

            fixed (char* ptr = builder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * builder.Length)); }
        }

        private URLAddress GetAssetBundleURL(bool hasSceneIDInPath, string hash, string sceneID, string assetBundleManifestVersion)
        {
            if (hasSceneIDInPath)
                return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{sceneID}/{hash}"));

            return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{hash}"));
        }

    }
}
