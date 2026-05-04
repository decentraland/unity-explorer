using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using DCL.Platforms;
using DCL.Utility;
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

                // Upstream code paths (e.g. PrepareGltfAssetLoadingSystem) pre-populate DepsDigest from the bare hash
                // before the platform suffix is appended. If they didn't, fall back to looking it up here, stripping
                // the platform suffix because the digest map is keyed by bare hashes.
                string? depsDigest = assetBundleIntention.DepsDigest;
                if (string.IsNullOrEmpty(depsDigest))
                {
                    string lookupHash = StripPlatformSuffix(assetBundleIntention.Hash);
                    if (assetBundleIntention.AssetBundleManifestVersion.TryGetDepsDigest(lookupHash, out string resolved))
                        depsDigest = resolved;
                    assetBundleIntention.DepsDigest = depsDigest;
                }

                assetBundleIntention.cacheHash = ComputeHash(assetBundleIntention.Hash,
                    assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestBuildDate(),
                    assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestVersion(),
                    depsDigest);
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));

        public unsafe Hash128 ComputeHash(string hash, string buildDate, string? version = null, string? depsDigest = null)
        {
            // For v49+ ABs the per-file deps digest replaces the buildDate sledgehammer that was previously used to
            // invalidate the cache when dependencies might have changed. Key on (version + hash + digest) instead.
            if (!string.IsNullOrEmpty(depsDigest))
            {
                string ver = version ?? string.Empty;
                Span<char> v49Builder = stackalloc char[ver.Length + hash.Length + depsDigest.Length];
                ver.AsSpan().CopyTo(v49Builder);
                hash.AsSpan().CopyTo(v49Builder[ver.Length..]);
                depsDigest.AsSpan().CopyTo(v49Builder[(ver.Length + hash.Length)..]);

                fixed (char* ptr = v49Builder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * v49Builder.Length)); }
            }

            Span<char> hashBuilder = stackalloc char[buildDate.Length + hash.Length];
            buildDate.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[buildDate.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        private static string StripPlatformSuffix(string hash)
        {
            string suffix = PlatformUtils.GetCurrentPlatform();
            return !string.IsNullOrEmpty(suffix) && hash.EndsWith(suffix, StringComparison.Ordinal)
                ? hash[..^suffix.Length]
                : hash;
        }

        private URLAddress GetAssetBundleURL(bool hasSceneIDInPath, string hash, string sceneID, string assetBundleManifestVersion)
        {
            if (hasSceneIDInPath)
                return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{sceneID}/{hash}"));

            return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{hash}"));
        }

    }
}
