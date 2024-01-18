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
                if (result.Succeeded)
                {
                    lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                    sceneLODInfo.CurrentLOD = new LODAsset(sceneLODInfo.GenerateCurrentLodKey(),
                        Object.Instantiate(result.Asset.GameObject, sceneLODInfo.ParcelPosition, Quaternion.identity),
                        result.Asset);
                }
                else
                {
                    if (sceneLODInfo.CurrentLODLevel.Equals(0))
                    {
                        lodCache.Dereference(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                        var lod0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        lod0.transform.position = sceneLODInfo.ParcelPosition;
                        lod0.transform.rotation = Quaternion.identity;
                        lod0.name = sceneLODInfo.GenerateCurrentLodKey();
                        sceneLODInfo.CurrentLOD = new LODAsset(sceneLODInfo.GenerateCurrentLodKey(),
                            lod0,
                            result.Asset);
                    }
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