using Arch.Core;
using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner;
using SceneRunner.Scene;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

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

            (_, ReadOnlyMemory<byte> mainCrdt) = await UniTask.WhenAll(loadSceneMetadata, loadMainCrdt);

            //TODO (JUANI): This can go away when we retrieve the version from the asset-bundle-registry
            AssetBundleManifestPromise loadAssetBundleManifest = await AssetBundleManifestPromise.Create(world,
                GetAssetBundleManifestIntention.Create(intention.DefinitionComponent.Definition.id, new CommonLoadingArguments(intention.DefinitionComponent.Definition.id)),
                partition).ToUniTaskAsync(world, cancellationToken: ct);

            if (loadAssetBundleManifest.Result.HasValue)
            {
                definitionComponent.Definition.assetBundleManifestVersion = loadAssetBundleManifest.Result.Value.Asset!.GetVersion();
                //If ABVersion is over 25 for scenes, the path will contain the scene hash
                definitionComponent.Definition.hasSceneInPath = int.Parse( definitionComponent.Definition.assetBundleManifestVersion.AsSpan().Slice(1)) >= 25;
            }
            else
            {
                //TODO (JUANI): What happens on fail?
            }

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);

            await UniTask.SwitchToMainThread();

            sceneFacade.Initialize();
            ReportHub.LogProductionInfo($"Loading scene {(sceneFacade.SceneData.IsPortableExperience() ? "(PX)" : "")} '{definition.GetLogSceneName()}' ended");
            return sceneFacade;
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
