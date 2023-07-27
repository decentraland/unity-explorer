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
        private readonly string assetBundleURL;

        public LoadSceneSystemLogic(string assetBundleURL)
        {
            this.assetBundleURL = assetBundleURL;
        }

        public async UniTask<ISceneFacade> Flow(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            UniTask<SceneAssetBundleManifest> loadAssetBundleManifest = LoadAssetBundleManifest(intention.IpfsPath.EntityId, reportCategory, ct);
            UniTask<string> loadSceneMetadata = OverrideSceneMetadata(intention, reportCategory, ct);

            (SceneAssetBundleManifest manifest, string contentBaseUrl) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata);

            // Create scene data
            var sceneData = new SceneData(intention.IpfsRealm, intention.Definition, true, manifest, IpfsHelper.DecodePointer(intention.Definition.metadata.scene.baseParcel), contentBaseUrl);

            // Calculate partition immediately

            await UniTask.SwitchToMainThread();

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifest(string sceneId, string reportCategory, CancellationToken ct)
        {
            var subIntent = new SubIntention(new CommonLoadingArguments($"{assetBundleURL}manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"));

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
        private async UniTask<string> OverrideSceneMetadata(GetSceneFacadeIntention intention, string reportCategory, CancellationToken ct)
        {
            IpfsTypes.SceneEntityDefinition definition = intention.Definition;

            string sceneJsonHash = null;

            foreach (IpfsTypes.ContentDefinition contentDefinition in definition.content)
            {
                if (contentDefinition.file != "scene.json") continue;

                sceneJsonHash = contentDefinition.hash;
                break;
            }

            if (sceneJsonHash == null)
                throw new ArgumentException("scene.json does not exist in the content");

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            string contentBaseUrl = string.IsNullOrEmpty(intention.IpfsPath.BaseUrl)
                ? intention.IpfsRealm.ContentBaseUrl
                : intention.IpfsPath.BaseUrl;

            var subIntent = new SubIntention(new CommonLoadingArguments(contentBaseUrl + sceneJsonHash));

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
            return contentBaseUrl;
        }
    }
}
