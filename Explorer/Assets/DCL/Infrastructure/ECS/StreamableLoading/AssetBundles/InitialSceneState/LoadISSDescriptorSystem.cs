using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
using DCL.WebRequests;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Resolves <see cref="ISSDescriptor"/> for a scene: fetches the descriptor JSON from
    ///     StreamingAssets and HEAD-probes the legacy ISS bundle URL to decide between
    ///     Bundle and Descriptor modes. Returns <see cref="ISSDescriptor.NONE"/> when no
    ///     descriptor JSON exists for the scene.
    /// </summary>
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadISSDescriptorSystem : LoadSystemBase<ISSDescriptor, GetISSDescriptor>
    {
        // ISS is only baked starting from AB manifest version 49. Anything older cannot have ISS, so
        // we short-circuit to NONE without touching the network.
        private const int MIN_ISS_AB_VERSION = 49;
        private const string DESCRIPTOR_SUBDIR = "iss_descriptors";

        private readonly IWebRequestController webRequestController;
        private readonly URLDomain streamingAssetURL;
        private readonly URLDomain assetBundleURL;

        internal LoadISSDescriptorSystem(World world, IWebRequestController webRequestController, URLDomain streamingAssetURL, URLDomain assetBundleURL, IStreamableCache<ISSDescriptor, GetISSDescriptor> cache)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.streamingAssetURL = streamingAssetURL;
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<ISSDescriptor>> FlowInternalAsync(GetISSDescriptor intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            // Skip network roundtrips entirely for AB versions that pre-date ISS support.
            if (!IsManifestVersionISSCapable(intention.ManifestVersion))
                return new StreamableLoadingResult<ISSDescriptor>(ISSDescriptor.NONE);

            ISSDescriptorMetadata? metadata = await TryLoadDescriptorAsync(intention.SceneId, ct);
            if (!metadata.HasValue)
                return new StreamableLoadingResult<ISSDescriptor>(ISSDescriptor.NONE);

            bool bundleReachable = await IsBundleReachableAsync(intention.SceneId, intention.ManifestVersion!, ct);

            var descriptor = new ISSDescriptor(
                bundleReachable ? ISSDescriptor.State.Bundle : ISSDescriptor.State.Descriptor,
                metadata.Value);

            return new StreamableLoadingResult<ISSDescriptor>(descriptor);
        }

        private static bool IsManifestVersionISSCapable(AssetBundleManifestVersion? manifestVersion)
        {
            if (manifestVersion == null || manifestVersion.assetBundleManifestRequestFailed) return false;

            string? version = manifestVersion.GetAssetBundleManifestVersion();
            if (string.IsNullOrEmpty(version) || version.Length < 2) return false;

            // Versions look like "v41" — strip the leading 'v' and compare numerically.
            return int.TryParse(version.AsSpan().Slice(1), out int versionNum) && versionNum >= MIN_ISS_AB_VERSION;
        }

        private async UniTask<ISSDescriptorMetadata?> TryLoadDescriptorAsync(string sceneId, CancellationToken ct)
        {
            URLAddress url = streamingAssetURL.Append(URLPath.FromString($"{DESCRIPTOR_SUBDIR}/{sceneId}_InitialSceneState.json"));

            try
            {
                return await webRequestController
                            .GetAsync(new CommonArguments(url), ct, GetReportData())
                            .CreateFromJson<ISSDescriptorMetadata>(WRJsonParser.Unity);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // No descriptor at the expected path — treat as no ISS.
                return null;
            }
        }

        private async UniTask<bool> IsBundleReachableAsync(string sceneId, AssetBundleManifestVersion manifestVersion, CancellationToken ct)
        {
            if (manifestVersion == null || manifestVersion.assetBundleManifestRequestFailed) return false;

            string version = manifestVersion.GetAssetBundleManifestVersion();
            if (string.IsNullOrEmpty(version)) return false;

            string bundleHash = $"staticscene_{sceneId}{PlatformUtils.GetCurrentPlatform()}";
            URLAddress bundleUrl = manifestVersion.HasHashInPath()
                ? assetBundleURL.Append(new URLPath($"{version}/{sceneId}/{bundleHash}"))
                : assetBundleURL.Append(new URLPath($"{version}/{bundleHash}"));

            return await webRequestController.IsHeadReachableAsync(GetReportData(), bundleUrl, ct, suppressErrors: true);
        }
    }
}
