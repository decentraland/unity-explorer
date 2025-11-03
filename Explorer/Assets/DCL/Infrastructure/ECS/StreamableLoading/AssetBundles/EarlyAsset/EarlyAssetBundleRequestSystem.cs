using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Utility;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.EarlyAsset
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    public partial class EarlyAssetBundleRequestSystem : BaseUnityLoopSystem
    {
        private bool requestDone;

        public EarlyAssetBundleRequestSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            if (!requestDone)
                StartEarlyDownloadQuery(World);
            ResolveEarlyDownloadQuery(World);
        }

        [Query]
        [All(typeof(EarlyAssetBundleFlag))]
        [None(typeof(AssetBundlePromise))]
        private void StartEarlyDownload(Entity entity, ref EarlyAssetBundleFlag earlySceneFlag)
        {
            AssetBundlePromise promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(GetAssetBundleIntention.BuildInitialSceneStateURL(earlySceneFlag.Scene.id),
                    assetBundleManifestVersion: earlySceneFlag.Scene.assetBundleManifestVersion,
                    parentEntityID: earlySceneFlag.Scene.id),
                PartitionComponent.TOP_PRIORITY);

            requestDone = true;
            World.Add(entity, promise);
        }


        [Query]
        [All(typeof(EarlyAssetBundleFlag))]
        private void ResolveEarlyDownload(Entity entity, ref AssetBundlePromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                //Do nothing. We just needed loaded in memory, we dont care the result.
                //Whoever needs it, will grab it later
                //TODO (JUANI) : Maybe we should instantiate it already?
                World.Destroy(entity);
                if (Result.Succeeded)
                {
                    //Dereferencing, because no one is using it yet
                    Result.Asset.Dereference();
                }
            }
        }

    }
}
