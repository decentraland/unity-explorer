using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceBudgeting.AcquiredBudget;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadWearableAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>
    {
        private readonly URLDomain assetBundleURL;

        private readonly URLBuilder urlBuilder = new ();

        internal LoadWearableAssetBundleManifestSystem(World world,
            IStreamableCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention> cache,
            MutexSync mutexSync, URLDomain assetBundleURL) : base(world, cache, mutexSync)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternalAsync(GetWearableAssetBundleManifestIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{intention.Hash}{PlatformUtils.GetPlatform()}.json"));

            string response;

            using (var request = UnityWebRequest.Get(urlBuilder.ToString()))
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
