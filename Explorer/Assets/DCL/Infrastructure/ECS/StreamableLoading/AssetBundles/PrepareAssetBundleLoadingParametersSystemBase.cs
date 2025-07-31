using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using UnityEditor;
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
                if (string.IsNullOrEmpty(assetBundleIntention.AssetBundleVersion) && !assetBundleIntention.SingleAssetBundleHack)
                {
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (GetReportCategory(), CreateException(new ArgumentException($"Manifest must be provided to load {assetBundleIntention.Name} from `WEB` source"))));

                    return;
                }

                /*
                TODO (JUANI) : This validation should be done in an upper level. It can go away if so;
                - For GLTF we do it in LoadGltfContainerSystem
                - And for textures? Where is that done? Do we even requests textures as ABs when not thumbanils?
                if (!assetBundleIntention.Manifest.Contains(assetBundleIntention.Hash))
                {
                    // Add the failure to the entity
                    World.Add(entity, new StreamableLoadingResult<AssetBundleData>
                        (GetReportCategory(), CreateException(new ArgumentException($"Asset Bundle {assetBundleIntention.Hash} {assetBundleIntention.Name} not found in the manifest"))));

                    return;
                }
                */

                if (assetBundleIntention.SingleAssetBundleHack)
                {
                    CommonLoadingArguments caHack = assetBundleIntention.CommonArguments;
                    caHack.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                    caHack.Timeout = StreamableLoadingDefaults.TIMEOUT;
                    caHack.CurrentSource = AssetSource.WEB;
                    caHack.URL = URLAddress.FromString(assetBundleIntention.Hash);
                    caHack.CacheableURL = URLAddress.FromString(assetBundleIntention.Hash);
                    assetBundleIntention.CommonArguments = caHack;
                    assetBundleIntention.cacheHash = Hash128.Compute(assetBundleIntention.Hash);
                    return;
                }

                CommonLoadingArguments ca = assetBundleIntention.CommonArguments;
                ca.Attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT;
                ca.Timeout = StreamableLoadingDefaults.TIMEOUT;
                ca.CurrentSource = AssetSource.WEB;
                ca.URL = GetAssetBundleURL(assetBundleIntention.HasParentEntityIDPathInURL, assetBundleIntention.Hash, assetBundleIntention.ParentEntityID, assetBundleIntention.AssetBundleVersion);
                ca.CacheableURL = GetCacheableURL(assetBundleIntention.Hash);
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.cacheHash = ComputeHash(assetBundleIntention.Hash, assetBundleIntention.AssetBundleVersion);
            }
        }

        private URLAddress GetStreamingAssetsUrl(string hash, URLSubdirectory customSubdirectory) =>

            // There is a special case when it comes to the shaders:
            // they are shared and custom subdirectory should be ignored, otherwise we would need to store a copy in every subdirectory
            customSubdirectory.IsEmpty() || COMMON_SHADERS.Contains(hash, StringComparer.OrdinalIgnoreCase)
                ? streamingAssetURL.Append(URLPath.FromString(hash))
                : streamingAssetURL.Append(customSubdirectory).Append(URLPath.FromString(hash));

        private unsafe Hash128 ComputeHash(string hash, string assetBundleManifestVersion)
        {
            //TODO (JUANI): Doing it like this we lose the ability to rebuild assets on a same version, since
            // new builds with the same version and hash will be incorrectly cached. Tolerated?
            Span<char> hashBuilder = stackalloc char[assetBundleManifestVersion.Length + hash.Length];
            assetBundleManifestVersion.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[assetBundleManifestVersion.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        private URLAddress GetAssetBundleURL(bool hasSceneIDInPath, string hash, string sceneID, string assetBundleManifestVersion)
        {
            if (hasSceneIDInPath)
                return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{sceneID}/{hash}"));

            return assetBundlesURL.Append(new URLPath($"{assetBundleManifestVersion}/{hash}"));
        }

        //Used for the OngoingRequests cache. We need to avoid version and sceneID in this URL to be able to reuse assets.
        //The first loaded hash will be the one used for all the other requests
        private URLAddress GetCacheableURL(string hash) =>
            assetBundlesURL.Append(new URLPath(hash));
    }
}
