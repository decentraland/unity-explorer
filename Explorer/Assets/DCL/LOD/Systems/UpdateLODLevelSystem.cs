using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneLODInfo))]
    public partial class UpdateLODLevelSystem : BaseUnityLoopSystem
    {
        private readonly LODCache lodCache;

        private readonly Vector2Int[] lodBucketLimits;

        public UpdateLODLevelSystem(World world, LODCache lodCache, Vector2Int[] lodBucketLimits) : base(world)
        {
            this.lodCache = lodCache;
            this.lodBucketLimits = lodBucketLimits;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
        }
        
        [Query]
        public void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent)
        {
            if (sceneLODInfo.IsDirty)
            {
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo);
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (partitionComponent.IsDirty)
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo);
        }
        
        [Query]
        public void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo)
        {
            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;
            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                    sceneLODInfo.CurrentLOD = new LODAsset(sceneLODInfo.GetCurrentLodKey(),
                        Object.Instantiate(result.Asset.GameObject, sceneLODInfo.ParcelPosition, Quaternion.identity),
                        result.Asset);
                }
            }
        }


        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            var sceneLODCandidate = 0;
            if (partitionComponent.Bucket > lodBucketLimits[0][0] &&
                partitionComponent.Bucket <= lodBucketLimits[0][1])
                sceneLODCandidate = 2;
            else if (partitionComponent.Bucket > lodBucketLimits[1][0])
                sceneLODCandidate = 3;

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            int sceneLODCandidate)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);

            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = sceneLODInfo.GetCurrentLodKey();
            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                sceneLODInfo.CurrentLOD = cachedAsset;
            }
            else
            {
                sceneLODInfo.CurrentLODPromise =
                    Promise.Create(World,
                        GetAssetBundleIntention.FromHash(newLODKey,
                            permittedSources: AssetSource.EMBEDDED,
                            customEmbeddedSubDirectory: URLSubdirectory.FromString("lods")),
                        partitionComponent);
            }
        }
    }
}