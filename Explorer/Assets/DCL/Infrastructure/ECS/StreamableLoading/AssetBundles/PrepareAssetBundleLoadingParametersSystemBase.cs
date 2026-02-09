using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.Abstract;
using Temp.Helper.WebClient;
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
#if UNITY_WEBGL
            "dcl/universal render pipeline/lit_ignore",
#else
            "dcl/scene_ignore_windows",
            "dcl/scene_ignore_mac",
            "dcl/universal render pipeline/lit_ignore_windows",
            "dcl/universal render pipeline/lit_ignore_mac",
#endif
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
                    WebGLDebugLog.Log("AB.Prepare", "WEB skipped: manifest null or failed", $"name={assetBundleIntention.Name} hash={assetBundleIntention.Hash}");
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (GetReportCategory(), CreateException(new ArgumentException($"Manifest version must be provided to load {assetBundleIntention.Name} from `WEB` source"))));

                    return;
                }

                string? version = assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestVersion();
                string? buildDate = assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestBuildDate();
                if (string.IsNullOrEmpty(version))
                    WebGLDebugLog.LogWarning("AB.Prepare", "GetAssetBundleManifestVersion() is null/empty", $"hash={assetBundleIntention.Hash}");

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                assetBundleIntention.Hash = assetBundleIntention.AssetBundleManifestVersion.CheckCasing(assetBundleIntention.Hash);
                ca.URL = GetAssetBundleURL(assetBundleIntention.AssetBundleManifestVersion.HasHashInPath(), assetBundleIntention.Hash, assetBundleIntention.ParentEntityID, version ?? "");
                assetBundleIntention.CommonArguments = ca;

                assetBundleIntention.cacheHash = ComputeHash(assetBundleIntention.Hash, buildDate ?? "");
                WebGLDebugLog.Log("AB.Prepare", "WEB", $"hash={assetBundleIntention.Hash} version={version} url={ca.URL.Value}");
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));

        public unsafe Hash128 ComputeHash(string hash, string buildDate)
        {
            Span<char> hashBuilder = stackalloc char[buildDate.Length + hash.Length];
            buildDate.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[buildDate.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        private URLAddress GetAssetBundleURL(bool hasSceneIDInPath, string hash, string sceneID, string assetBundleManifestVersion)
        {
            if (hasSceneIDInPath)
                return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{sceneID}/{hash}"));

            return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{hash}"));
        }

    }
}
