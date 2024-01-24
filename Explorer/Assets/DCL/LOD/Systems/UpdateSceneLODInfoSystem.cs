using System.Collections.Generic;
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
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.SceneBoundsChecker;
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
        private readonly ILODAssetsPool lodCache;

        private readonly List<int> lodBucketThresholds;

        public readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;

        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, List<int> lodBucketThresholds,
            IPerformanceBudget memoryBudget, IPerformanceBudget frameCapBudget) : base(world)
        {
            this.lodCache = lodCache;
            this.lodBucketThresholds = lodBucketThresholds;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
        }


        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
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
        [None(typeof(DeleteEntityIntention))]
        public void ResolveCurrentLODPromise(in Entity entity, ref SceneLODInfo sceneLODInfo)
        {
            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    lodCache.Release(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                    var instantiatedLOD = Object.Instantiate(result.Asset.GameObject, sceneLODInfo.ParcelPosition,
                        Quaternion.identity);
                    ConfigureSceneMaterial.EnableSceneBounds(in instantiatedLOD,
                        in sceneLODInfo.SceneCircumscribedPlanes);
                    sceneLODInfo.CurrentLOD = new LODAsset(sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash,
                        instantiatedLOD, result.Asset);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    //TODO: Add a default LOD so we dont have to fail the promise every time
                }
            }
        }


        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            var sceneLODCandidate = 0;

            for (var i = 0; i < lodBucketThresholds.Count; i++)
            {
                if (partitionComponent.Bucket >= lodBucketThresholds[i])
                    sceneLODCandidate = i;
            }

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            int sceneLODCandidate)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = $"{sceneLODInfo.SceneHash}_{sceneLODInfo.CurrentLODLevel}";

            //If the current LOD is the candidate, no need to make a new promise or set anything new
            if (newLODKey.Equals(sceneLODInfo.CurrentLOD.LodKey)) return;
            
            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                lodCache.Release(sceneLODInfo.CurrentLOD.LodKey, sceneLODInfo.CurrentLOD);
                sceneLODInfo.CurrentLOD = cachedAsset;
            }
            else
            {
                //TODO: TEMP, for some reason genesis plaza asset is crashing in mac
                if ((Application.platform.Equals(RuntimePlatform.OSXPlayer) ||
                     Application.platform.Equals(RuntimePlatform.OSXEditor)) &&
                    sceneLODInfo.SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                {
                    return;
                }

                //TODO: TEMP, infinite floor sceene
                if (sceneLODInfo.SceneHash.Equals("bafkreictb7lsedstowe2twuqjk7nn3hvqs3s2jqhc2bduwmein73xxelbu"))
                {
                    return;
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
