using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
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
    ///     Resolves the descriptor JSON for a scene: fetches it from the LOD manifest bucket and hands the
    ///     parsed <see cref="ISSDescriptorMetadata"/> back to the consumer. Scenes without ISS data
    ///     (pre-v49 manifest or missing descriptor JSON) yield a *failed* result — that way the framework
    ///     never persists the "no ISS" outcome to disk (PutAsync is gated on success), and the consumer
    ///     in <c>ResolveISSDescriptorSystem</c> calls <c>ISSDescriptor.MarkAsNone</c>.
    /// </summary>
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadISSDescriptorSystem : LoadSystemBase<ISSDescriptorMetadata, GetISSDescriptorIntention>
    {
        private const string DESCRIPTOR_PATH_PREFIX = "lods-unity/manifests/";

        private readonly IWebRequestController webRequestController;
        private readonly URLDomain descriptorBaseUrl;

        internal LoadISSDescriptorSystem(World world, IWebRequestController webRequestController,
            URLDomain descriptorBaseUrl,
            IStreamableCache<ISSDescriptorMetadata, GetISSDescriptorIntention> cache, DiskCacheOptions<ISSDescriptorMetadata, GetISSDescriptorIntention>? diskCacheOptions = null)
            : base(world, cache, diskCacheOptions)
        {
            this.webRequestController = webRequestController;
            this.descriptorBaseUrl = descriptorBaseUrl;
        }

        protected override async UniTask<StreamableLoadingResult<ISSDescriptorMetadata>> FlowInternalAsync(GetISSDescriptorIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            // Feature-flag gate: when disabled, short-circuit with a failed result so the consumer
            // in ResolveISSDescriptorSystem treats every scene as "no ISS" and falls back to the
            // pre-ISS code path. Same shape as the SupportsISS check below — failed result is not
            // persisted to disk by LoadSystemBase.
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.NEW_LODS))
                return new StreamableLoadingResult<ISSDescriptorMetadata>(GetReportData(), new Exception("new-lods feature flag disabled"));

            // Skip network roundtrips entirely for AB versions that pre-date ISS support. Returning a
            // failed result (plain Exception, not StreamableLoadingException) keeps the disk cache clean
            // — LoadSystemBase only persists on success — and doesn't spam logs from the result ctor.
            if (!intention.ManifestVersion.SupportsISS())
                return new StreamableLoadingResult<ISSDescriptorMetadata>(GetReportData(), new Exception("ISS unsupported for this manifest version"));

            ISSDescriptorMetadata? metadata = await TryLoadDescriptorAsync(intention.SceneId, ct);
            if (!metadata.HasValue)
                return new StreamableLoadingResult<ISSDescriptorMetadata>(GetReportData(), new Exception("No ISS descriptor JSON for this scene"));

            return new StreamableLoadingResult<ISSDescriptorMetadata>(metadata.Value);
        }

        private async UniTask<ISSDescriptorMetadata?> TryLoadDescriptorAsync(string sceneId, CancellationToken ct)
        {
            URLAddress url = descriptorBaseUrl.Append(URLPath.FromString($"{DESCRIPTOR_PATH_PREFIX}{sceneId}_InitialSceneState.json"));

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
    }
}
