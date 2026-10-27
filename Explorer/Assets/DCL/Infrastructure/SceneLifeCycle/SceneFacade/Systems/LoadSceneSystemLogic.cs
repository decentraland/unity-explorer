using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.SceneRunner.Scene;
using DCL.Utilities.Extensions;
using DCL.Utility.Exceptions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using SceneRunner;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic
    {
        private const string SCENE_JSON = "scene.json";
        private const string MAIN_CRDT = "main.crdt";

        private readonly IWebRequestController webRequestController;
        private readonly URLDomain assetBundleURL;
        private readonly bool localSceneDevelopment;

        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, bool localSceneDevelopment = false)
        {
            this.webRequestController = webRequestController;
            this.assetBundleURL = assetBundleURL;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public async UniTask<ISceneFacade> FlowAsync(World world, ISceneFactory sceneFactory, GetSceneFacadeIntention intention, ReportData reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            var definitionComponent = intention.DefinitionComponent;
            var ipfsPath = definitionComponent.IpfsPath;
            var definition = definitionComponent.Definition;

            ReportHub.LogProductionInfo( $"Loading scene '{definition?.GetLogSceneName()}' began");

            var hashedContent = await GetSceneHashedContentAsync(definition, ipfsPath.BaseUrl, reportCategory, ct);
            // First process the scene metadata.
            // Fixes possible race conditions with the setup of the scene definition
            await OverrideSceneMetadataAsync(hashedContent, intention, reportCategory, ipfsPath.EntityId, ct);
            await UniTask.SwitchToMainThread(ct);

            ReadOnlyMemory<byte> mainCrdt = await LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt),
                intention.ISSDescriptor);

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, new AllowEverythingJsApiPermissionsProvider(), partition, ct);

            await UniTask.SwitchToMainThread();

            sceneFacade.Initialize();
            ReportHub.LogProductionInfo($"Loading scene {(sceneFacade.SceneData.IsPortableExperience() ? "(PX)" : "")} '{definition.GetLogSceneName()}' (sdk version: '{sceneData.GetSDKVersion()}') ended");
            return sceneFacade;
        }

        private async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory, CancellationToken ct)
        {
            var hashedContent = new SceneHashedContent(definition.content!, contentBaseUrl);

            string? abVersion = definition.assetBundleManifestVersion?.GetAssetBundleManifestVersion();

            // In local scene development the scene's code must always come from the live preview
            // server, never be overridden by CDN copies (the local AB server serves real manifests,
            // so the version check alone no longer filters these scenes out).
            if (localSceneDevelopment || string.IsNullOrEmpty(abVersion) || string.IsNullOrEmpty(definition.metadata?.main))
                return hashedContent;

            await TryOverrideWithCDNAsync(hashedContent, definition, abVersion, reportCategory, ct);

            return hashedContent;
        }

        private async UniTask TryOverrideWithCDNAsync(SceneHashedContent hashedContent, SceneEntityDefinition definition, string abVersion, ReportData reportCategory, CancellationToken ct)
        {
            string mainFileName = definition.metadata.main;
            var cdnBasePath = $"{abVersion}/{definition.id}/";

            URLAddress mainUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{mainFileName}"));
            URLAddress sceneJsonUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{SCENE_JSON}"));
            URLAddress mainCrdtUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{MAIN_CRDT}"));

            // Check main script + fixed CDN files in parallel with HEAD requests
            (bool mainReachable, bool sceneJsonReachable, bool mainCrdtReachable) = await UniTask.WhenAll(
                webRequestController.IsHeadReachableAsync(reportCategory, mainUrl, ct).SuppressAnyExceptionWithFallback(false),
                webRequestController.IsHeadReachableAsync(reportCategory, sceneJsonUrl, ct).SuppressAnyExceptionWithFallback(false),
                webRequestController.IsHeadReachableAsync(reportCategory, mainCrdtUrl, ct).SuppressAnyExceptionWithFallback(false));

            if (mainReachable && sceneJsonReachable && mainCrdtReachable)
            {
                hashedContent.OverrideContentUrl(mainFileName, mainUrl);
                hashedContent.OverrideContentUrl(SCENE_JSON, sceneJsonUrl);
                hashedContent.OverrideContentUrl(MAIN_CRDT, mainCrdtUrl);
            }
        }

        private async UniTask<ReadOnlyMemory<byte>> LoadMainCrdtAsync(ISceneContent sceneContent, ReportData reportCategory, CancellationToken ct)
        {
            // CDN-first resolution is handled by URL overrides injected into SceneHashedContent when available.
            if (!sceneContent.TryGetContentUrl(MAIN_CRDT, out URLAddress url))
                return ReadOnlyMemory<byte>.Empty;

            return await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory).GetDataCopyAsync();
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        private async UniTask<bool> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, ReportData reportCategory, string sceneID, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl(SCENE_JSON, out var sceneJsonUrl))
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
