using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadWearableAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>
    {
        private readonly string assetBundleURL;

        internal LoadWearableAssetBundleManifestSystem(World world,
            IStreamableCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention> cache,
            MutexSync mutexSync, string assetBundleURL) : base(world, cache, mutexSync)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternal(GetWearableAssetBundleManifestIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string response;

            using (var request = UnityWebRequest.Get($"{assetBundleURL}manifest/{intention.Hash}{PlatformUtils.GetPlatform()}.json"))
            {
                await request.SendWebRequest().WithCancellation(ct);

                if (request.result != UnityWebRequest.Result.Success)
                    return new StreamableLoadingResult<SceneAssetBundleManifest>(new Exception($"Failed to load asset bundle manifest for intention: {intention.Hash}"));

                response = request.downloadHandler.text;
            }

            //Deserialize out of the main thread
            await UniTask.SwitchToThreadPool();
            return new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest(assetBundleURL, JsonUtility.FromJson<SceneAbDto>(response)));
        }
    }
}
