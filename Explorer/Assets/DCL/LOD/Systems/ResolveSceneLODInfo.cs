using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveSceneLODInfo : BaseUnityLoopSystem
    {
        private readonly LODCache lodCache;

        public ResolveSceneLODInfo(World world, LODCache lodCache) : base(world)
        {
            this.lodCache = lodCache;
        }

        protected override void Update(float t)
        {
            ResolveLODInfoQuery(World);
        }

        //TODO: Once we have manifest ABS, it will be resolved in this system
        [Query]
        private void ResolveLODInfo(ref SceneLODInfo sceneLODInfo,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty) return;

            sceneLODInfo.CurrentLODLevel = -1;
            sceneLODInfo.SceneHash = sceneDefinitionComponent.Definition.id;
            sceneLODInfo.ParcelPosition =
                ParcelMathHelper.GetPositionByParcelPosition(sceneDefinitionComponent.Parcels[0]);
            sceneLODInfo.LodCache = lodCache;
        }
    }
}