using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearablesDTOSystem : LoadSystemBase<WearableDTO, GetWearableIntention>
    {
        private readonly string ASSET_BUNDLE_URL;

        public LoadWearablesDTOSystem(World world, IStreamableCache<WearableDTO, GetWearableIntention> cache, MutexSync mutexSync, string assetBundleURL) : base(world, cache, mutexSync)
        {
            ASSET_BUNDLE_URL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<WearableDTO>> FlowInternal(GetWearableIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string bodyRequest = "{\"pointers\":[\"" + intention.Pointer + "\"]}";

            string response;

            using (var request = UnityWebRequest.Post(intention.CommonArguments.URL, bodyRequest, "application/json"))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            await UniTask.SwitchToThreadPool();
            var targetList = new List<WearableDTO>();
            JsonConvert.PopulateObject(response, targetList);
            WearableDTO result = targetList[0];

            await UniTask.SwitchToMainThread();
            SceneAssetBundleManifest assetBundleManifest = await LoadAssetBundleManifest(result.id, "WearableLoading", ct);
            result.AssetBundleManifest = assetBundleManifest;

            return new StreamableLoadingResult<WearableDTO>(result);
        }

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifest(string sceneId, string reportCategory, CancellationToken ct)
        {
            var subIntent = new SubIntention(new CommonLoadingArguments($"{ASSET_BUNDLE_URL}manifest/{sceneId}{PlatformUtils.GetPlatform()}.json"));

            // Repeat loop for this request only
            async UniTask<StreamableLoadingResult<string>> InnerFlow(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(subIntention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(wr.downloadHandler.text);
            }

            StreamableLoadingResult<string> result = (await subIntent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, InnerFlow, reportCategory, ct)).Denullify();

            if (result.Succeeded)
            {
                await UniTask.SwitchToThreadPool();
                return new SceneAssetBundleManifest(ASSET_BUNDLE_URL, JsonUtility.FromJson<SceneAbDto>(result.Asset));
            }

            // Don't block the scene if the loading manifest failed, just use NULL
            ReportHub.LogError(new ReportData(reportCategory, ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {sceneId}");
            return SceneAssetBundleManifest.NULL;
        }
    }
}
