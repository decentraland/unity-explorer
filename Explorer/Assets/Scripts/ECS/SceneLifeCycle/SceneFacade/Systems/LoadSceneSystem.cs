using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Cache;
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
using Utility;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle
{
    /// <summary>
    ///     Loads and starts a scene from scene and realm definitions
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneSystem : LoadSystemBase<ISceneFacade, GetSceneFacadeIntention>
    {
        private readonly string assetBundleURL;
        private readonly ISceneFactory sceneFactory;

        internal LoadSceneSystem(World world, string assetBundleURL,
            ISceneFactory sceneFactory, IStreamableCache<ISceneFacade, GetSceneFacadeIntention> cache,
            MutexSync mutexSync, IConcurrentBudgetProvider concurrentBudgetProvider) : base(world, cache, mutexSync, concurrentBudgetProvider)
        {
            this.assetBundleURL = assetBundleURL;
            this.sceneFactory = sceneFactory;
        }

        protected override async UniTask<StreamableLoadingResult<ISceneFacade>> FlowInternal(GetSceneFacadeIntention intention, CancellationToken ct)
        {
            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            UniTask<SceneAssetBundleManifest> loadAssetBundleManifest = LoadAssetBundleManifest(intention.IpfsPath.EntityId, ct);
            UniTask<string> loadSceneMetadata = OverrideSceneMetadata(intention, ct);

            (SceneAssetBundleManifest manifest, string contentBaseUrl) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata);

            await UniTask.SwitchToMainThread();

            return new StreamableLoadingResult<ISceneFacade>(await sceneFactory.CreateSceneFromSceneDefinition(intention.IpfsRealm, intention.Definition, manifest, contentBaseUrl, ct));
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifest(string sceneId, CancellationToken ct)
        {
            var subIntent = new SubIntention(new CommonLoadingArguments($"{assetBundleURL}manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"));

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlow(SubIntention subIntention, CancellationToken ct)
            {
                UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            StreamableLoadingResult<string> result = (await subIntent.RepeatLoop(InnerFlow, GetReportCategory(), ct)).Denullify();

            if (result.Succeeded)
            {
                // Parse off the main thread
                await UniTask.SwitchToThreadPool();
                return new SceneAssetBundleManifest(assetBundleURL, JsonUtility.FromJson<SceneAbDto>(result.Asset));
            }

            // Don't block the scene if the loading manifest failed, just use NULL
            ReportHub.LogError(new ReportData(GetReportCategory(), ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {sceneId}");
            return SceneAssetBundleManifest.NULL;
        }

        /// <summary>
        ///     Loads scene metadata from a separate endpoint to ensure it contains "baseUrl" and overrides the existing metadata
        ///     with new one
        /// </summary>
        private async UniTask<string> OverrideSceneMetadata(GetSceneFacadeIntention intention, CancellationToken ct)
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
            async UniTask<StreamableLoadingResult<string>> InnerFlow(SubIntention subIntention, CancellationToken ct)
            {
                UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            string result = (await subIntent.RepeatLoop(InnerFlow, GetReportCategory(), ct)).UnwrapAndRethrow();

            await UniTask.SwitchToThreadPool();

            // Parse the JSON
            JsonUtility.FromJsonOverwrite(result, intention.Definition.metadata);
            intention.Definition.id = intention.IpfsPath.EntityId;
            return contentBaseUrl;
        }
    }
}
