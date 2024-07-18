using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
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
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly Transform lodsTransformParent;
        private GameObjectPool<LODGroup> lodGroupPool;

        public UpdateSceneLODInfoSystem(World world, GameObjectPool<LODGroup> lodGroupPool, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, Transform lodsTransformParent) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodsTransformParent = lodsTransformParent;
            this.lodGroupPool = lodGroupPool;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!partitionComponent.IsBehind) // Only want to load scene in our direction of travel
            {
                // LOD distances are currently using the old system so will only load in the LOD when the gameobject
                // is in the correct bucket. Once the lods are in it will change LODs based on screenspace size in relation
                // to height and dither the transition.
                byte lodForAcquisition = CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
                LODKey newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, lodForAcquisition);
                if (sceneLODInfo.LODAssets.Count == 0) // If no lods have been loaded, assume required
                {
                    AddLODAsset(ref sceneLODInfo, ref partitionComponent, newLODKey, lodForAcquisition);
                }
                else // otherwise check the requested LODkey doesn't already exist and add to list
                {
                    if (!sceneLODInfo.HasLODKey(newLODKey)) //If the current LOD is the candidate, no need to make a new promise or set anything new
                    {
                        AddLODAsset(ref sceneLODInfo, ref partitionComponent, newLODKey, lodForAcquisition);
                    }
                }
            }
        }

        private void AddLODAsset(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, LODKey newLODKey, byte lodForAcquisition)
        {
            LODAsset tempLODAsset = null;
            if (lodCache.TryGet(newLODKey, out var cachedAsset)) // Try to get from the cache of previously loaded/unloaded LODAssets
            {
                // If its cached, no need to make a new promise
                tempLODAsset = cachedAsset;
                sceneLODInfo.LODAssets.Add(cachedAsset);

                if (cachedAsset.lodGO != null) // Previous promise might not have been completed before removal, or promise failed and need retrying
                {
                    // ...otherwise re-parent to the LODGroup entity and re-evaluate the LODGroup
                    Transform lodGroupTransform = sceneLODInfo.CreateLODGroup(lodGroupPool, lodsTransformParent);
                    cachedAsset.lodGO.transform.SetParent(lodGroupTransform);
                    sceneLODInfo.ReEvaluateLODGroup();
                }
            }
            else
            {
                LODAsset lodAsset = new LODAsset(newLODKey, lodCache);
                lodAsset.currentLODLevel = lodForAcquisition; // All LODAssets are marked to their LOD level for order sorting
                tempLODAsset = lodAsset;
                sceneLODInfo.LODAssets.Add(lodAsset);
            }

            if (tempLODAsset.State != LODAsset.LOD_STATE.SUCCESS) // Create promise if not already loaded.
            {
                string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
                var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];

                var assetBundleIntention = GetAssetBundleIntention.FromHash(typeof(GameObject),
                    platformLODKey,
                    permittedSources: AssetSource.ALL,
                    customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                    manifest: manifest);

                tempLODAsset.LODPromise = Promise.Create(World, assetBundleIntention, partitionComponent);
            }
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (IsLOD0(ref sceneLODInfo))
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }
        }

        private byte CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            return sceneLODCandidate;
        }

        private bool IsLOD0(ref SceneLODInfo sceneLODInfo)
        {
            //return sceneLODInfo.CurrentLOD[0].LodKey.Level == 0;
            return true;
        }
    }
}
