using Arch.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.SceneRunner.Scene;
using DCL.Utility;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.InitialSceneState;
using SceneRunner;
using SceneRunner.Scene;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

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

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            var contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            var hashedContent = await GetSceneHashedContentAsync(definition, contentBaseUrl, reportCategory);
            UniTask<UniTaskVoid> loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, reportCategory, ipfsPath.EntityId, ct);
            var loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);
            var ISSContainedAssetsPromise = LoadISS(world, definition, ct);

            (_, var mainCrdt, var ISSAssets) = await UniTask.WhenAll(loadSceneMetadata, loadMainCrdt, ISSContainedAssetsPromise);

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt),
                ISSAssets);

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);

            await UniTask.SwitchToMainThread();

            sceneFacade.Initialize();
            ReportHub.LogProductionInfo($"Loading scene {(sceneFacade.SceneData.IsPortableExperience() ? "(PX)" : "")} '{definition.GetLogSceneName()}' ended");
            return sceneFacade;
        }

        private async UniTask<IInitialSceneState> LoadISS(World world, SceneEntityDefinition sceneDefinitionComponent, CancellationToken ct)
        {
            if (sceneDefinitionComponent.SupportInitialSceneState())
            {
                var promise = AssetBundlePromise.Create(world,
                    GetAssetBundleIntention.FromHash($"staticscene_{sceneDefinitionComponent.id}{PlatformUtils.GetCurrentPlatform()}",
                        assetBundleManifestVersion: sceneDefinitionComponent.assetBundleManifestVersion,
                        parentEntityID: sceneDefinitionComponent.id),
                    PartitionComponent.TOP_PRIORITY);

                promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

                if (promise.Result.Value.Succeeded)
                {
                    var result = new HashSet<string>();

                    foreach (string s in promise.Result.Value.Asset.InitialSceneStateMetadata.Value.assetHash)
                        result.Add($"{s}{PlatformUtils.GetCurrentPlatform()}");

                    return InitialSceneStateInfo.CreateISS(promise.Result.Value.Asset, result);
                }
            }

            return InitialSceneStateInfo.CreateEmpty();
        }

        protected abstract string GetAssetBundleSceneId(string ipfsPathEntityId);

        protected abstract UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory);

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
        protected async UniTask<UniTaskVoid> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, ReportData reportCategory, string sceneID, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out var sceneJsonUrl))
            {
                //What happens if we dont have a scene.json file? Will the default one work?
                ReportHub.LogWarning(reportCategory.WithStaticDebounce(), $"scene.json does not exist for scene {sceneID}, no override is possible");
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
