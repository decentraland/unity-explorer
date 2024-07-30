using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(UpdateSceneLODInfoSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class InitializeSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly ILODCache lodCache;
        private readonly int lodLevels;


        public InitializeSceneLODInfoSystem(World world, ILODCache lodCache, int lodLevels) : base(world)
        {
            this.lodLevels = lodLevels;
            this.lodCache = lodCache;
        }

        protected override void Update(float t)
        {
            InitializeSceneLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InitializeSceneLOD(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            // Means its already initialized
            if (!string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            string sceneID = sceneDefinitionComponent.Definition.id;
            sceneLODInfo.metadata = lodCache.Get(sceneID, lodLevels);
            sceneLODInfo.id = sceneID;
        }


    }
}