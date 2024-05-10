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

        public LoadSceneSystemLogicBase(IWebRequestController webRequestController, URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        public abstract UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct);

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