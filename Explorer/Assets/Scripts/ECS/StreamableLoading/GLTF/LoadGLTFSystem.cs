using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    //[LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private ISceneData sceneData;

        internal LoadGLTFSystem(
            World world,
            IStreamableCache<GLTFData, GetGLTFIntention> cache,
            ISceneData sceneData) : base(world, cache)
        {
            this.sceneData = sceneData;
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            GLTFData data = new (intention.Name);

            Debug.Log($"sceneData.SceneContent.ContentBaseUrl+intention.Name: {sceneData.SceneContent.ContentBaseUrl+intention.Name}");
            using (UnityWebRequest webRequest = new UnityWebRequest(sceneData.SceneContent.ContentBaseUrl+intention.Name))
            {
                // ((DownloadHandlerAssetBundle)webRequest.downloadHandler).autoLoadAssetBundle = false;
                // await webRequest.SendWebRequest().WithCancellation(ct);
                //
                // using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                //     assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);
                //
                // // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
                // acquiredBudget.Release();
                //
                // // if GetContent prints an error, null will be thrown
                // if (assetBundle == null)
                //     throw new NullReferenceException($"{intention.Hash} Asset Bundle is null: {webRequest.downloadHandler.error}");
            }



            return new Common.Components.StreamableLoadingResult<GLTFData>(data);
        }

    }
}
