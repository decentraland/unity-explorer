using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadSceneSystemLogicBase
    {
        protected readonly URLDomain assetBundleURL;
        protected readonly IWebRequestController webRequestController;

        public LoadSceneSystemLogicBase(IWebRequestController webRequestController, URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        public async UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            var definitionComponent = intention.DefinitionComponent;
            var ipfsPath = definitionComponent.IpfsPath;
            var definition = definitionComponent.Definition;

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            var contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            var hashedContent = await GetSceneHashedContent(definition.content, contentBaseUrl, reportCategory);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            var loadAssetBundleManifest = LoadAssetBundleManifestAsync(GetAssetBundleSceneId(ipfsPath.EntityId), reportCategory, ct);
            var loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, ipfsPath.EntityId, reportCategory, ct);
            var loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            (var manifest, _, var mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, manifest, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }

        protected abstract string GetAssetBundleSceneId(string ipfsPathEntityId);

        protected abstract UniTask<ISceneContent> GetSceneHashedContent(List<ContentDefinition>? definition, URLDomain contentBaseUrl, string reportCategory);

        protected async UniTask<ReadOnlyMemory<byte>> LoadMainCrdtAsync(ISceneContent sceneContent, string reportCategory, CancellationToken ct)
        {
            const string NAME = "main.crdt";

            // if scene does not contain main.crdt, do nothing
            if (!sceneContent.TryGetContentUrl(NAME, out var url))
                return ReadOnlyMemory<byte>.Empty;

            return await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory).GetDataCopyAsync();
        }

        protected async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string sceneId, string reportCategory, CancellationToken ct)
        {
            var url = assetBundleURL.Append(URLPath.FromString($"manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"));

            try
            {
                var sceneAbDto = await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory)
                    .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                if (sceneAbDto.ValidateVersion())
                    return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.files);

                ReportHub.LogError(new ReportData(reportCategory, ReportHint.SessionStatic), $"Asset Bundle Version Mismatch for {sceneId}");
                return SceneAssetBundleManifest.NULL;
            }
            catch
            {
                // Don't block the scene if the loading manifest failed, just use NULL
                ReportHub.LogError(new ReportData(reportCategory, ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {sceneId}");
                return SceneAssetBundleManifest.NULL;
            }
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        protected async UniTask<UniTaskVoid> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, string reportCategory, string sceneID, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out var sceneJsonUrl))
            {
                //What happens if we dont have a scene.json file? Will the default one work?
                ReportHub.LogWarning(new ReportData(reportCategory, ReportHint.SessionStatic), $"scene.json does not exist for scene {sceneID}, no override is possible");
                return default;
            }

            var target = intention.DefinitionComponent.Definition.metadata;

            await webRequestController.GetAsync(new CommonArguments(sceneJsonUrl), ct, reportCategory)
                .OverwriteFromJsonAsync(target, WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            intention.DefinitionComponent.Definition.id = intention.DefinitionComponent.IpfsPath.EntityId;

            return default;
        }
    }
}