using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;


namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly ILODAssetsPool lodCache;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        internal IPerformanceBudget frameCapBudget;


        private readonly Transform lodsTransformParent;
        private readonly TextureArrayContainer lodTextureArrayContainer;


        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IPerformanceBudget memoryBudget, IPerformanceBudget frameCapBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue,
            Transform lodsTransformParent, TextureArrayContainer lodTextureArrayContainer) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodsTransformParent = lodsTransformParent;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
            //InstantiateCurrentLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //New LOD infront of you. Update
            if (!partitionComponent.IsBehind && sceneLODInfo.CurrentLODLevel == byte.MaxValue)
            {
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
                return;
            }

            //Existing LOD (either infront or behind you). Update
            if (partitionComponent.IsDirty && sceneLODInfo.CurrentLODLevel != byte.MaxValue)
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
        }


        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InstantiateCurrentLOD(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty || sceneLODInfo.CurrentLOD == null) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            var currentLOD = sceneLODInfo.CurrentLOD;

            if (currentLOD.State == LODAsset.LOD_STATE.WAITING_INSTANTIATION &&
                currentLOD.AsyncInstantiation.IsWaitingForSceneActivation())
            {
                FinalizeAsyncInstantiation(currentLOD, sceneDefinitionComponent);
                sceneLODInfo.UpdateCurrentVisibleLOD();
                CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty || sceneLODInfo.CurrentLODPromise.IsConsumed) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                LODAsset newLod = default;
                if (result.Succeeded)
                {
                    //NOTE (JUANI): Using the count API since the one without count does not parent correctly.
                    //ANOTHER NOTE: InstantiateAsync has an issue with SMR assignation. Its a Unity bug (https://issuetracker.unity3d.com/issues/instantiated-prefabs-recttransform-values-are-incorrect-when-object-dot-instantiateasync-is-used)
                    //we cannot fix, so we'll use Instantiate until solved.
                    //var asyncInstantiation =
                    //    Object.InstantiateAsync(result.Asset!.GetMainAsset<GameObject>(),1,
                    //        lodsTransformParent, sceneDefinitionComponent.SceneGeometry.BaseParcelPosition, Quaternion.identity);
                    //asyncInstantiation.allowSceneActivation = false;
                    //newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                    //    lodCache, result.Asset, asyncInstantiation);


                    //Remove everything down here once Unity fixes AsyncInstantiation
                    //Uncomment everything above here and the InstantiateCurrentLODQuery
                    var instantiatedLOD = Object.Instantiate(result.Asset!.GetMainAsset<GameObject>(),
                        sceneDefinitionComponent.SceneGeometry.BaseParcelPosition, Quaternion.identity, lodsTransformParent);
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                        lodCache, result.Asset, null);
                    FinalizeInstantiation(newLod, sceneDefinitionComponent, instantiatedLOD);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel), lodCache);
                }

                sceneLODInfo.SetCurrentLOD(newLod);
                CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
            }
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.CurrentLOD.LodKey.Level == 0)
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }
            sceneLODInfo.IsDirty = false;
        }

        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate, sceneDefinitionComponent);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            byte sceneLODCandidate, SceneDefinitionComponent sceneDefinitionComponent)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel);

            //If the current LOD is the candidate, no need to make a new promise or set anything new
            if (newLODKey.Equals(sceneLODInfo.CurrentLOD))
            {
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (newLODKey.Equals(sceneLODInfo.CurrentVisibleLOD))
            {
                sceneLODInfo.ResetToCurrentVisibleLOD();
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                sceneLODInfo.SetCurrentLOD(cachedAsset);
                CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
                return;
            }

            string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
            var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];

            var assetBundleIntention =  GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: AssetSource.ALL,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest);

            sceneLODInfo.CurrentLODPromise =
                Promise.Create(World, assetBundleIntention, partitionComponent);

            sceneLODInfo.IsDirty = true;
        }

        private void FinalizeAsyncInstantiation(LODAsset currentLOD, SceneDefinitionComponent sceneDefinitionComponent)
        {
            currentLOD.AsyncInstantiation.allowSceneActivation = true;
            currentLOD.AsyncInstantiation.WaitForCompletion();
            var newRoot = currentLOD.AsyncInstantiation.Result[0];
            FinalizeInstantiation(currentLOD, sceneDefinitionComponent, newRoot);
        }

        private void FinalizeInstantiation(LODAsset currentLOD, SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD)
        {
            var slots = Array.Empty<TextureArraySlot?>();
            if (!currentLOD.LodKey.Level.Equals(0))
            {
                slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.Definition.id,
                    sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, instantiatedLOD, lodTextureArrayContainer);
            }

            currentLOD?.FinalizeInstantiation(instantiatedLOD, slots);
        }

    }
}
