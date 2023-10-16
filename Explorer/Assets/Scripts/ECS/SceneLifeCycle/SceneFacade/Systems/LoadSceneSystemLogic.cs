using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic
    {
        private readonly URLDomain assetBundleURL;

        public LoadSceneSystemLogic(URLDomain assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
        }

        public async UniTask<ISceneFacade> Flow(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            URLDomain contentBaseUrl = intention.IpfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : intention.IpfsPath.BaseUrl;

            var hashedContent = new SceneHashedContent(intention.Definition.content, contentBaseUrl);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            UniTask<SceneAssetBundleManifest> loadAssetBundleManifest = LoadAssetBundleManifest(intention.IpfsPath.EntityId, reportCategory, ct);
            UniTask<UniTaskVoid> loadSceneMetadata = OverrideSceneMetadata(hashedContent, intention, reportCategory, ct);
            UniTask<ReadOnlyMemory<byte>> loadMainCrdt = LoadMainCrdt(hashedContent, reportCategory, ct);

            (SceneAssetBundleManifest manifest, _, ReadOnlyMemory<byte> mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Create scene data
            var sceneData = new SceneData(hashedContent, intention.Definition, manifest, IpfsHelper.DecodePointer(intention.Definition.metadata.scene.baseParcel), new StaticSceneMessages(mainCrdt));

            // Calculate partition immediately

            await UniTask.SwitchToMainThread();

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }

        private async UniTask<ReadOnlyMemory<byte>> LoadMainCrdt(ISceneContent sceneContent, string reportCategory, CancellationToken ct)
        {
            const string NAME = "main.crdt";

            // if scene does not contain main.crdt, do nothing
            if (!sceneContent.TryGetContentUrl(NAME, out URLAddress url))
                return ReadOnlyMemory<byte>.Empty;

            var subIntent = new SubIntention(new CommonLoadingArguments(url));

            static async UniTask<StreamableLoadingResult<ReadOnlyMemory<byte>>> InnerFlow(SubIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(intention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<ReadOnlyMemory<byte>>(wr.downloadHandler.data);
            }

            return (await subIntent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlow, reportCategory, ct)).UnwrapAndRethrow();
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifest(string sceneId, string reportCategory, CancellationToken ct)
        {
            var subIntent = new SubIntention(new CommonLoadingArguments(assetBundleURL.Append(URLPath.FromString($"manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"))));

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlow(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            StreamableLoadingResult<string> result = (await subIntent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlow, reportCategory, ct)).Denullify();

            if (result.Succeeded)
            {
                // Parse off the main thread
                await UniTask.SwitchToThreadPool();
                return new SceneAssetBundleManifest(assetBundleURL, JsonUtility.FromJson<SceneAbDto>(result.Asset));
            }

            // Don't block the scene if the loading manifest failed, just use NULL
            ReportHub.LogError(new ReportData(reportCategory, ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {sceneId}");
            return SceneAssetBundleManifest.NULL;
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        private async UniTask<UniTaskVoid> OverrideSceneMetadata(ISceneContent sceneContent, GetSceneFacadeIntention intention, string reportCategory, CancellationToken ct)
        {
            const string NAME = "scene.json";

            if (!sceneContent.TryGetContentUrl(NAME, out URLAddress sceneJsonUrl))
                throw new ArgumentException("scene.json does not exist in the content");

            var subIntent = new SubIntention(new CommonLoadingArguments(sceneJsonUrl));

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlow(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            string result = (await subIntent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlow, reportCategory, ct)).UnwrapAndRethrow();

            await UniTask.SwitchToThreadPool();

            // Parse the JSON
            JsonUtility.FromJsonOverwrite(result, intention.Definition.metadata);
            intention.Definition.id = intention.IpfsPath.EntityId;
            return default(UniTaskVoid);
        }
    }
}
