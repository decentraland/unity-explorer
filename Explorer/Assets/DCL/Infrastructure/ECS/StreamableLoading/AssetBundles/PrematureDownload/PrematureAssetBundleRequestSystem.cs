using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DefaultNamespace
{

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PrematureAssetBundleRequestSystem : BaseUnityLoopSystem
    {
        private bool requestDone;

        public PrematureAssetBundleRequestSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            if (!requestDone)
            {
                AssetBundlePromise promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash("GP_staticscene_LZMA_StaticSceneDescriptor"),
                    PartitionComponent.TOP_PRIORITY);
                requestDone = true;
                World.Create(promise, new PrematureDownloadComponent());
                UnityEngine.Debug.Log("JUANI THE PREMATURE REQUEST WAS DONE");
            }
            CompletePrematureDownloadsQuery(World);
        }

        [Query]
        [All(typeof(PrematureDownloadComponent))]
        private void CompletePrematureDownloads(Entity entity, ref AssetBundlePromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                //Do nothing. We just needed loaded in memory, we dont care the result.
                //Whoever needs it, will grab it later
                UnityEngine.Debug.Log("JUANI THE PREMATURE LOAD WAS DONE");
                World.Destroy(entity);
            }

        }
    }
}
