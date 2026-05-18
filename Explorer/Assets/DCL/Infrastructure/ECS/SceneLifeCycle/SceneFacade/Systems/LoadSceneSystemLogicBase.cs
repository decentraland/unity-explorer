using Arch.Core;
using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility.Exceptions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using SceneRunner;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using ISSDescriptorPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.InitialSceneState.ISSDescriptor, ECS.StreamableLoading.AssetBundles.InitialSceneState.GetISSDescriptor>;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadSceneSystemLogicBase
    {
        protected readonly URLDomain assetBundleURL;
        protected readonly IWebRequestController webRequestController;

        protected LoadSceneSystemLogicBase(IWebRequestController webRequestController, URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        public async UniTask<ISceneFacade> FlowAsync(World world, ISceneFactory sceneFactory, GetSceneFacadeIntention intention, ReportData reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            var definitionComponent = intention.DefinitionComponent;
            var ipfsPath = definitionComponent.IpfsPath;
            var definition = definitionComponent.Definition;

            ReportHub.LogProductionInfo( $"Loading scene '{definition?.GetLogSceneName()}' began");

            var hashedContent = await GetSceneHashedContentAsync(definition, ipfsPath.BaseUrl, reportCategory, ct);
            // First process the scene metadata.
            // Fixes possible race conditions with the setup of the scene definition, especially on Hybrid mode (LSD+remote ABs)
            await OverrideSceneMetadataAsync(hashedContent, intention, reportCategory, ipfsPath.EntityId, ct);
            await UniTask.SwitchToMainThread(ct);

            // Both tasks start eagerly so they run concurrently; if ISS is in play, also prefetches the
            // shared Bundle-mode AB on the descriptor so the scene's GLTF requests hit the cache.
            UniTask<ReadOnlyMemory<byte>> loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);
            UniTask loadISS = ResolveAndPrefetchISSAsync(world, definition, partition, ct);
            ReadOnlyMemory<byte> mainCrdt = await loadMainCrdt;
            await loadISS;

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, new AllowEverythingJsApiPermissionsProvider(), partition, ct);

            await UniTask.SwitchToMainThread();

            sceneFacade.Initialize();
            ReportHub.LogProductionInfo($"Loading scene {(sceneFacade.SceneData.IsPortableExperience() ? "(PX)" : "")} '{definition.GetLogSceneName()}' (sdk version: '{sceneData.GetSDKVersion()}') ended");
            return sceneFacade;
        }

        /// <summary>
        ///     Lazily resolves the ISS descriptor (gated by v49+ manifest) and, if Bundle mode is
        ///     selected, eagerly fetches the shared ISS asset bundle and attaches it to the descriptor
        ///     so it stays cached for the LOD path and SDK GLTF requests rewritten to its URL.
        ///     No-op for pre-v49 scenes and for descriptors that have no shared bundle.
        /// </summary>
        private async UniTask ResolveAndPrefetchISSAsync(World world, SceneEntityDefinition definition, IPartitionComponent partition, CancellationToken ct)
        {
            GetISSDescriptor intention = GetISSDescriptor.For(definition);

            if (!ISSDescriptorCache.INSTANCE.TryGet(intention, out ISSDescriptor descriptor))
            {
                ISSDescriptorPromise promise = ISSDescriptorPromise.Create(world, intention, partition);
                promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

                descriptor = promise.Result is { Succeeded: true } result ? result.Asset! : ISSDescriptor.NONE;
            }

            if (!descriptor.SupportsBundle()) return;

            AssetBundlePromise bundlePromise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(GetAssetBundleIntention.BuildInitialSceneStateURL(definition.id),
                    assetBundleManifestVersion: definition.assetBundleManifestVersion,
                    parentEntityID: definition.id),
                PartitionComponent.TOP_PRIORITY);

            bundlePromise = await bundlePromise.ToUniTaskAsync(world, cancellationToken: ct);

            if (bundlePromise.Result.Value.Succeeded)
                descriptor.AttachAssetBundle(bundlePromise.Result.Value.Asset);
        }

        protected abstract string GetAssetBundleSceneId(string ipfsPathEntityId);

        protected abstract UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory, CancellationToken ct);

        protected async UniTask<ReadOnlyMemory<byte>> LoadMainCrdtAsync(ISceneContent sceneContent, ReportData reportCategory, CancellationToken ct)
        {
            const string NAME = "main.crdt";

            // if scene does not contain main.crdt, do nothing
            if (!sceneContent.TryGetContentUrl(NAME, out var url))
                return ReadOnlyMemory<byte>.Empty;

            return await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory).GetDataCopyAsync();
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        protected virtual async UniTask<bool> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, ReportData reportCategory, string sceneID, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out var sceneJsonUrl))
            {
                //What happens if we dont have a scene.json file? Will the default one work?
                ReportHub.LogWarning(reportCategory.WithStaticDebounce(), $"scene.json does not exist for scene {sceneID}, no override is possible");
                return false;
            }

            var target = intention.DefinitionComponent.Definition.metadata;

            try
            {
                await webRequestController.GetAsync(new CommonArguments(sceneJsonUrl), ct, reportCategory)
                                          .OverwriteFromJsonAsync(target, WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);
            }
            catch (UnityWebRequestException ex)
            {
                if (ex.ResponseCode == WebRequestUtils.NOT_FOUND)
                    throw new ManifestNotFoundException($"Scene manifest not found for scene {sceneID} from {sceneJsonUrl}: {ex.Message}");
            }

            intention.DefinitionComponent.Definition.id = intention.DefinitionComponent.IpfsPath.EntityId;

            return true;
        }
    }
}
