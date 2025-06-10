using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using SceneRunner;
using SceneRunner.Scene;
using Utility;

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

        public async UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, ReportData reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            var definitionComponent = intention.DefinitionComponent;
            var ipfsPath = definitionComponent.IpfsPath;
            var definition = definitionComponent.Definition;

            ReportHub.LogProductionInfo( $"Loading scene '{definition?.GetLogSceneName()}' began");

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            var contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            ISceneContent? hashedContent = await GetSceneHashedContentAsync(definition, contentBaseUrl, reportCategory, ct);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            var loadAssetBundleManifest = LoadAssetBundleManifestAsync(GetAssetBundleSceneId(ipfsPath.EntityId), reportCategory, ct);
            UniTask<UniTaskVoid> loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, reportCategory, ipfsPath.EntityId, ct);
            var loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            (SceneAssetBundleManifest manifest, _, ReadOnlyMemory<byte> mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, manifest, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);

            await UniTask.SwitchToMainThread();

            sceneFacade.Initialize();
            ReportHub.LogProductionInfo($"Loading scene '{definition.GetLogSceneName()}' ended");
            return sceneFacade;
        }

        protected abstract string GetAssetBundleSceneId(string ipfsPathEntityId);

        protected abstract UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory, CancellationToken ct);

        protected async UniTask<ReadOnlyMemory<byte>> LoadMainCrdtAsync(ISceneContent sceneContent, ReportData reportCategory, CancellationToken ct)
        {
            const string NAME = "main.crdt";

            // if scene does not contain main.crdt, do nothing
            if (!sceneContent.TryGetContentUrl(NAME, out var url))
                return ReadOnlyMemory<byte>.Empty;

            return await webRequestController.GetAsync(new CommonArguments(url), reportCategory).GetDataCopyAsync(ct);
        }

        protected async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string sceneId, ReportData reportCategory, CancellationToken ct)
        {
            var url = assetBundleURL.Append(URLPath.FromString($"manifest/{sceneId}{PlatformUtils.GetCurrentPlatform()}.json"));

            try
            {
                SceneAbDto sceneAbDto = await webRequestController.GetAsync(new CommonArguments(url), reportCategory)
                                                                  .CreateFromJsonAsync<SceneAbDto>(WRJsonParser.Unity, ct, WRThreadFlags.SwitchToThreadPool);

                if (AssetValidation.ValidateSceneAbDto(sceneAbDto, AssetValidation.WearableIDError, sceneId))
                    return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.files, sceneId, sceneAbDto.Date);

                ReportHub.LogError(reportCategory.WithSessionStatic(), $"Asset Bundle Version Mismatch for {sceneId}");
                return SceneAssetBundleManifest.NULL;
            }
            catch (Exception e)
            {
                // Don't block the scene if the loading manifest failed, just use NULL
                if (e is not OperationCanceledException)
                    ReportHub.LogError(reportCategory.WithSessionStatic(), $"Asset Bundles Manifest is not loaded for scene {sceneId}");

                return SceneAssetBundleManifest.NULL;
            }
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        protected async UniTask<UniTaskVoid> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, ReportData reportCategory, string sceneID, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out var sceneJsonUrl))
            {
                //What happens if we dont have a scene.json file? Will the default one work?
                ReportHub.LogWarning(reportCategory.WithSessionStatic(), $"scene.json does not exist for scene {sceneID}, no override is possible");
                return default;
            }

            var target = intention.DefinitionComponent.Definition.metadata;

            await webRequestController.GetAsync(new CommonArguments(sceneJsonUrl), reportCategory)
                                      .OverwriteFromJsonAsync(target, WRJsonParser.Unity, ct, WRThreadFlags.SwitchToThreadPool);

            intention.DefinitionComponent.Definition.id = intention.DefinitionComponent.IpfsPath.EntityId;

            return default;
        }
    }
}
