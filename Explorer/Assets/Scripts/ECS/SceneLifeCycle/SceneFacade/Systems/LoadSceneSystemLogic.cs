using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceBudgeting;
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
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic
    {
        private readonly URLDomain assetBundleURL;

        public LoadSceneSystemLogic(URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
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
            Vector2Int baseParcel = IpfsHelper.DecodePointer(definition.metadata.scene.baseParcel);
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

            var subIntent = new SubIntention(new CommonLoadingArguments(url));

            return (await subIntent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlowAsync, reportCategory, ct)).UnwrapAndRethrow();

            static async UniTask<StreamableLoadingResult<ReadOnlyMemory<byte>>> InnerFlowAsync(SubIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(intention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<ReadOnlyMemory<byte>>(wr.downloadHandler.data);
            }
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string sceneId, string reportCategory, CancellationToken ct)
        {
            var subIntent = new SubIntention(new CommonLoadingArguments(assetBundleURL.Append(URLPath.FromString($"manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"))));

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlowAsync(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            StreamableLoadingResult<string> result = (await subIntent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlowAsync, reportCategory, ct)).Denullify();

            if (result.Succeeded)
            {
                // Parse off the main thread
                await UniTask.SwitchToThreadPool();
                SceneAbDto sceneAbDto = JsonUtility.FromJson<SceneAbDto>(result.Asset);

                if (sceneAbDto.ValidateVersion())
                    return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto);
            }

            // Don't block the scene if the loading manifest failed, just use NULL
            ReportHub.LogError(new ReportData(reportCategory, ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {sceneId}");
            return SceneAssetBundleManifest.NULL;
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

            var subIntent = new SubIntention(new CommonLoadingArguments(sceneJsonUrl));

            string result = (await subIntent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlowAsync, reportCategory, ct)).UnwrapAndRethrow();

            await UniTask.SwitchToThreadPool();

            // Parse the JSON
            JsonUtility.FromJsonOverwrite(result, intention.DefinitionComponent.Definition.metadata);
            intention.DefinitionComponent.Definition.id = intention.DefinitionComponent.IpfsPath.EntityId;
            return default(UniTaskVoid);

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlowAsync(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }
        }
    }
}
