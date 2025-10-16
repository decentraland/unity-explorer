using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DefaultNamespace
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EarlyAssetBundleRequestSystem : BaseUnityLoopSystem
    {
        private bool requestDone;

        public EarlyAssetBundleRequestSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            StartEarlyDownloadQuery(World);
            ResolveEarlyDownloadQuery(World);
        }

        [Query]
        [All(typeof(EarlyAssetBundleFlag))]
        [None(typeof(AssetBundlePromise))]
        private void StartEarlyDownload(Entity entity, ref EarlyAssetBundleFlag earlySceneFlag)
        {
            AssetBundlePromise promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(earlySceneFlag.AsssetBundleHash),
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

                if (Result.Succeeded) { Debug.Log("JUANI THE ASSET BUNDLE WAS LOADED IN MEMORY"); }
            }
        }

    }
}
