using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private readonly string assetBundleURL;

        public LoadAssetBundleManifestSystem(World world, IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache,
            string assetBundleURL, MutexSync mutexSync, IConcurrentBudgetProvider loadingBudgetProvider)
            : base(world, cache, mutexSync, loadingBudgetProvider)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternal(GetAssetBundleManifestIntention intention, CancellationToken ct)
        {
            var wr = UnityWebRequest.Get(intention.CommonArguments.URL);
            await wr.SendWebRequest().WithCancellation(ct);

            string text = wr.downloadHandler.text;

            // Parse off the main thread
            await UniTask.SwitchToThreadPool();

            return new StreamableLoadingResult<SceneAssetBundleManifest>(
                new SceneAssetBundleManifest(assetBundleURL, JsonUtility.FromJson<SceneAbDto>(text)));
        }
    }
}
