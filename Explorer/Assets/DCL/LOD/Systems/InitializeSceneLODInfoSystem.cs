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
        private readonly IComponentPool<LODGroup> lodGroupsPool;
        private readonly Transform lodCacheParent;


        public InitializeSceneLODInfoSystem(World world, ILODCache lodCache, int lodLevels, IComponentPool<LODGroup> lodGroupsPool, Transform lodCacheParent) : base(world)
        {
            this.lodLevels = lodLevels;
            this.lodGroupsPool = lodGroupsPool;
            this.lodCacheParent = lodCacheParent;
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

            string sceneID = sceneDefinitionComponent.Definition.id!;

            if (lodCache.TryGet(sceneID, out var cacheInfo))
                sceneLODInfo.metadata = cacheInfo;
            else
                sceneLODInfo.metadata = new LODCacheInfo(InitializeLODGroup(sceneID), lodLevels);
            sceneLODInfo.id = sceneID;
        }

        private LODGroup InitializeLODGroup(string sceneID)
        {
            var newLODGroup = lodGroupsPool.Get();
            newLODGroup.name = $"LODGroup_{sceneID}";
            newLODGroup.transform.SetParent(lodCacheParent);
            return newLODGroup;
        }


    }
}