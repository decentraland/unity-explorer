using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Ipfs;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Threading;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic
    {
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        public async UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            SceneDefinitionComponent definitionComponent = intention.DefinitionComponent;
            IpfsTypes.IpfsPath ipfsPath = definitionComponent.IpfsPath;
            IpfsTypes.SceneEntityDefinition definition = definitionComponent.Definition;

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            URLDomain contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            var hashedContent = new SceneHashedContent(definition.content, contentBaseUrl);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            UniTask<SceneAssetBundleManifest> loadAssetBundleManifest = LoadAssetBundleManifestAsync(ipfsPath.EntityId, reportCategory, ct);
            UniTask<UniTaskVoid> loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, reportCategory, ct);
            UniTask<ReadOnlyMemory<byte>> loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            (SceneAssetBundleManifest manifest, _, ReadOnlyMemory<byte> mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Create scene data
            var baseParcel = intention.Definition.metadata.scene.DecodedBase;
            ParcelMathHelper.SceneGeometry sceneGeometry = ParcelMathHelper.CreateSceneGeometry(intention.DefinitionComponent.ParcelsCorners, baseParcel);
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, manifest, baseParcel, sceneGeometry, new StaticSceneMessages(mainCrdt));

            // Calculate partition immediately

            await UniTask.SwitchToMainThread();

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }

        private async UniTask<ReadOnlyMemory<byte>> LoadMainCrdtAsync(ISceneContent sceneContent, string reportCategory, CancellationToken ct)
        {
            const string NAME = "main.crdt";

            // if scene does not contain main.crdt, do nothing
            if (!sceneContent.TryGetContentUrl(NAME, out URLAddress url))
                return ReadOnlyMemory<byte>.Empty;

            return (await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory)).GetDataCopy();
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string sceneId, string reportCategory, CancellationToken ct)
        {
            URLAddress url = assetBundleURL.Append(URLPath.FromString($"manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"));

            try
            {
                SceneAbDto sceneAbDto = await (await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory))
                   .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                if (sceneAbDto.ValidateVersion())
                    return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto);

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
        private async UniTask<UniTaskVoid> OverrideSceneMetadataAsync(ISceneContent sceneContent, GetSceneFacadeIntention intention, string reportCategory, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out URLAddress sceneJsonUrl))
                throw new ArgumentException("scene.json does not exist in the content");

            IpfsTypes.SceneMetadata target = intention.Definition.metadata;

            await (await webRequestController.GetAsync(new CommonArguments(sceneJsonUrl), ct, reportCategory))
               .OverwriteFromJsonAsync(target, WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            intention.DefinitionComponent.Definition.id = intention.DefinitionComponent.IpfsPath.EntityId;

            return default(UniTaskVoid);
        }
    }
}
