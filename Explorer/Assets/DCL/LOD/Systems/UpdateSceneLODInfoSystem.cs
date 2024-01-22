using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Primitives;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneLODInfo))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly LODAssetCache lodCache;

        private readonly Vector2Int[] lodBucketLimits;

        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;

        public UpdateSceneLODInfoSystem(World world, LODAssetCache lodCache, Vector2Int[] lodBucketLimits,
            IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.lodCache = lodCache;
            this.lodBucketLimits = lodBucketLimits;
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
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
            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;
            
            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;
            
            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                var lodKey = sceneLODInfo.GenerateCurrentLodKey();
                if (result.Succeeded)
                {
                    lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                    sceneLODInfo.CurrentLOD = new LODAsset(lodKey,
                        Object.Instantiate(result.Asset.GameObject, sceneLODInfo.ParcelPosition, Quaternion.identity),
                        result.Asset);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(), $"LOD request for {lodKey} failed");
                    //TODO: Add a default LOD so we dont have to fail the promise every time
                }
            }
        }


        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            var sceneLODCandidate = 0;
            
            if (partitionComponent.Bucket > lodBucketLimits[0][0] &&
                partitionComponent.Bucket <= lodBucketLimits[0][1])
                sceneLODCandidate = 1;
            else if (partitionComponent.Bucket > lodBucketLimits[1][0])
                sceneLODCandidate = 2;

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            int sceneLODCandidate)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);

            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = sceneLODInfo.GenerateCurrentLodKey();
            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                sceneLODInfo.CurrentLOD = cachedAsset;
            }
            else
            {
                //TODO: TEMP, for some reason genesis plaza asset is crashing in mac
                if ((Application.platform.Equals(RuntimePlatform.OSXPlayer) ||
                     Application.platform.Equals(RuntimePlatform.OSXEditor)) &&
                    sceneLODInfo.SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                {
                    sceneLODInfo.SceneHash = "FAIL_THIS_REQUEST_IN_MAC";
                    newLODKey = sceneLODInfo.GenerateCurrentLodKey();
                }

                //TODO: TEMP, infinite floor sceene
                if (sceneLODInfo.SceneHash.Equals("bafkreictb7lsedstowe2twuqjk7nn3hvqs3s2jqhc2bduwmein73xxelbu"))
                {
                    sceneLODInfo.SceneHash = "FAIL_THIS_INFINTIE_FLOOR_REQUEST";
                    newLODKey = sceneLODInfo.GenerateCurrentLodKey();
                }
                
                
                
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