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
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
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


        public InstantiateSceneLODInfoSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainer lodTextureArrayContainer) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
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
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) // Don't process promises if budget is maxxed out
                return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    var instantiatedLOD = Object.Instantiate(result.Asset!.GetMainAsset<GameObject>(),
                        sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                        Quaternion.identity);
                    var newLod = new LODAsset(instantiatedLOD, result.Asset,
                        GetTextureSlot(sceneLODInfo.CurrentLODLevelPromise, sceneDefinitionComponent.Definition, instantiatedLOD));

                    CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
                    sceneLODInfo.AddSuccessLOD(instantiatedLOD, newLod, defaultFOV, defaultLodBias);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(), $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
                    sceneLODInfo.AddFailedLOD();
                }
            }
        }

        private TextureArraySlot?[] GetTextureSlot(byte lodLevel, SceneEntityDefinition sceneDefinitionComponent, GameObject instantiatedLOD)
        {
            var slots = Array.Empty<TextureArraySlot?>();
            if (!lodLevel.Equals(0))
                slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.id, sceneDefinitionComponent.metadata.scene.DecodedBase, instantiatedLOD, lodTextureArrayContainer);
            return slots;
        }


        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.CurrentLODLevelPromise == 0)
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }
        }
        

    }
}