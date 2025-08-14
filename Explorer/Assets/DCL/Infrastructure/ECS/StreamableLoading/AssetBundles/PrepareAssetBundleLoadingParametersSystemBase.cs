using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
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
                ca.URL = GetAssetBundleURL(assetBundleIntention.AssetBundleManifestVersion.HasHashInPath(), assetBundleIntention.Hash, assetBundleIntention.ParentEntityID, assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestVersion());
                assetBundleIntention.CommonArguments = ca;
                assetBundleIntention.Hash = CheckCapitalizationFix(assetBundleIntention.Hash);
                assetBundleIntention.cacheHash = ComputeHash(assetBundleIntention.Hash, assetBundleIntention.AssetBundleManifestVersion.GetAssetBundleManifestBuildDate());
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

        private string CheckCapitalizationFix(string inputHash)
        {
            // TODO (JUANI): hack, for older Qm assets. Doesnt happen with bafk because they are all lowercase
            // This has a long due capitalization problem. The hash which is requested should always be lower case, since the output files are lowercase and the
            // request to S3 is case sensitive.
            // IE: This works: https://ab-cdn.decentraland.org/v35/Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk/qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_mac
            //     This doesnt: https://ab-cdn.decentraland.org/v35/Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk/QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV_mac
            // This was previously fixes using this extension (https://github.com/decentraland/unity-explorer/blob/7dd332562143e406fecf7006ac86586add0b0c71/Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/SceneAssetBundleManifestExtensions.cs#L5)
            // But we cannot use it anymore since we are not downloading the whole manifest
            // Maybe one day, when `Qm` deployments dont exist anymore, this method can be removed
            var span = inputHash.AsSpan();
            return (span.Length >= 2 && span[0] == 'Q' && span[1] == 'm')
                ? inputHash.ToLowerInvariant()
                : inputHash;
        }

    }
}
