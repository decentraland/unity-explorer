using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using UnityEngine;
using Utility;
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
                byte lodForAcquisition = GetLODLevelForPartition(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
                if (!sceneLODInfo.HasLODLoaded(lodForAcquisition))
                    StartLODPromise(ref sceneLODInfo, ref partitionComponent, sceneDefinitionComponent, lodForAcquisition);
            }
        }

        private void StartLODPromise(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, byte level)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
            var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, level);

            string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
            var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];

            var assetBundleIntention = GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: AssetSource.ALL,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest);

            sceneLODInfo.CurrentLODLevelPromise = level;
            sceneLODInfo.CurrentLODPromise = Promise.Create(World, assetBundleIntention, partitionComponent);
        }

        private byte GetLODLevelForPartition(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            if (sceneLODInfo.fScreenRelativeTransitionHeight >= 0.3f && sceneLODCandidate == 1)
                sceneLODCandidate = 0;

            return sceneLODCandidate;
        }
    }
}
