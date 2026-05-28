using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.SceneRunner.Scene;
using DCL.Utility;
using DCL.WebRequests;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Resolves the <see cref="ISSDescriptorResolution"/> for a scene: fetches the static scene descriptor
    ///     JSON from the LOD manifest bucket and always returns Descriptor mode. Returns
    ///     <see cref="ISSDescriptorResolution.NONE"/> when no descriptor JSON exists for the scene.
    ///     <para>
    ///     The HEAD probe that chooses between Bundle and Descriptor modes is temporarily disabled — see
    ///     <see cref="IsBundleReachableAsync"/>. A later PR will re-enable Bundle mode.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadISSDescriptorSystem : LoadSystemBase<ISSDescriptorResolution, GetISSDescriptor>
    {
        // Hardcoded for this iteration — wire to DI once the dev/prod bucket split lands.
        private static readonly URLDomain DESCRIPTOR_BASE_URL =
            URLDomain.FromString("https://lod-unity-bucket-dev-0871c25.s3.us-east-1.amazonaws.com/lods-unity/manifests/");

        private readonly IWebRequestController webRequestController;
        private readonly URLDomain assetBundleURL;

        internal LoadISSDescriptorSystem(World world, IWebRequestController webRequestController, URLDomain assetBundleURL,
            IStreamableCache<ISSDescriptorResolution, GetISSDescriptor> cache, DiskCacheOptions<ISSDescriptorResolution, GetISSDescriptor>? diskCacheOptions = null)
            : base(world, cache, diskCacheOptions)
        {
            this.webRequestController = webRequestController;
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<ISSDescriptorResolution>> FlowInternalAsync(GetISSDescriptor intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            // Skip network roundtrips entirely for AB versions that pre-date ISS support.
            if (!intention.ManifestVersion.SupportsISS())
                return new StreamableLoadingResult<ISSDescriptorResolution>(ISSDescriptorResolution.NONE);

            ISSDescriptorMetadata? metadata = await TryLoadDescriptorAsync(intention.SceneId, ct);
            if (!metadata.HasValue)
                return new StreamableLoadingResult<ISSDescriptorResolution>(ISSDescriptorResolution.NONE);

            // Bundle-mode HEAD probe is temporarily disabled — every ISS-capable scene goes through
            // Descriptor mode for now. Kept the IsBundleReachableAsync helper below so re-enabling is
            // a one-line restore in a later PR; see PR description for rationale.
            // bool bundleReachable = await IsBundleReachableAsync(intention.SceneId, intention.ManifestVersion!, ct);

            return new StreamableLoadingResult<ISSDescriptorResolution>(
                new ISSDescriptorResolution(IISSDescriptor.State.Descriptor, metadata.Value.assets));
        }

        private async UniTask<ISSDescriptorMetadata?> TryLoadDescriptorAsync(string sceneId, CancellationToken ct)
        {
            URLAddress url = DESCRIPTOR_BASE_URL.Append(URLPath.FromString($"{sceneId}_InitialSceneState.json"));

            try
            {
                // Missing-descriptor is the expected case for non-ISS scenes — suppress the underlying
                // 403/404 log so it doesn't spam every realm load. The catch below still treats the
                // failure as "no ISS for this scene."
                return await webRequestController
                            .GetAsync(new CommonArguments(url), ct, GetReportData(), suppressErrors: true)
                            .CreateFromJson<ISSDescriptorMetadata>(WRJsonParser.Unity);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // No descriptor at the expected path — treat as no ISS.
                return null;
            }
        }

        // Currently unused: FlowInternalAsync forces Descriptor mode. Kept for the follow-up PR that
        // re-enables Bundle-mode selection.
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
