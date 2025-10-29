using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class InstantiateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly TextureArrayContainer lodTextureArrayContainer;
        internal IPerformanceBudget frameCapBudget;
        private float defaultFOV;
        private float defaultLodBias;

        private readonly IRealmPartitionSettings realmPartitionSettings;


        public InstantiateSceneLODInfoSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainer lodTextureArrayContainer, IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public override void Initialize()
        {
            defaultFOV = World.CacheCamera().GetCameraComponent(World).Camera.fieldOfView;
            defaultLodBias = QualitySettings.lodBias;
        }

        protected override void Update(float t)
        {
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent, in InitialSceneStateDescriptor initialSceneStateDescriptor)
        {
            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) // Don't process promises if budget is maxxed out
                return;

            if (!sceneLODInfo.HasActiveLODPromise())
                return;

            //Means it has already been resolved
            if (sceneLODInfo.IsLODInstantiated(sceneLODInfo.CurrentLODLevelPromise))
                return;

            if (initialSceneStateDescriptor.IsValid() && sceneLODInfo.CurrentLODLevelPromise == 0)
                ResolveInitialSceneStateDescriptorLOD(initialSceneStateDescriptor, sceneDefinitionComponent, ref sceneLODInfo);
            else
                ResolveSceneLOD(sceneDefinitionComponent, ref sceneLODInfo);
        }

        private void ResolveInitialSceneStateDescriptorLOD(in InitialSceneStateDescriptor initialSceneStateDescriptor, in SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            if (!initialSceneStateDescriptor.IsDownloadedAndReady())
                return;

            var instantiatedLOD = new GameObject($"Static_LOD_{sceneDefinitionComponent.Definition.id}");
            initialSceneStateDescriptor.RepositionStaticAssets(instantiatedLOD);
            instantiatedLOD.transform.position = sceneDefinitionComponent.SceneGeometry.BaseParcelPosition;


            var newLod = new LODAsset(instantiatedLOD, initialSceneStateDescriptor.AssetBundleData.Asset,
                GetTextureSlot(sceneLODInfo.CurrentLODLevelPromise, sceneDefinitionComponent.Definition, instantiatedLOD));

            //Adding a manual reference since we didnt get this object through the regular AB flow
            initialSceneStateDescriptor.AssetBundleData.Asset.AddReference();
            sceneLODInfo.AddSuccessLOD(instantiatedLOD, newLod, defaultFOV, defaultLodBias, realmPartitionSettings.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
        }

        private void ResolveSceneLOD(in SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    var instantiatedLOD = Object.Instantiate(result.Asset!.GetAsset<GameObject>(),
                        sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                        Quaternion.identity);

                    //TODO (JUANI) : Remove before merge. Jsut leaving it here for showing the LOD_1 of new GP
                    instantiatedLOD.gameObject.SetActive(true);
                    var newLod = new LODAsset(instantiatedLOD, result.Asset,
                        GetTextureSlot(sceneLODInfo.CurrentLODLevelPromise, sceneDefinitionComponent.Definition, instantiatedLOD));

                    sceneLODInfo.AddSuccessLOD(instantiatedLOD, newLod, defaultFOV, defaultLodBias, realmPartitionSettings.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    sceneLODInfo.AddFailedLOD();
                }

                LODUtils.TryReportSDK6SceneLoadedForLOD(sceneLODInfo, sceneDefinitionComponent, sceneReadinessReportQueue,
                    scenesCache);
            }
        }



        private TextureArraySlot?[] GetTextureSlot(byte lodLevel, SceneEntityDefinition sceneDefinitionComponent, GameObject instantiatedLOD)
        {
            var slots = Array.Empty<TextureArraySlot?>();
            if (!lodLevel.Equals(0))
                slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.id, sceneDefinitionComponent.metadata.scene.DecodedBase, instantiatedLOD, lodTextureArrayContainer);
            return slots;
        }

    }
}
