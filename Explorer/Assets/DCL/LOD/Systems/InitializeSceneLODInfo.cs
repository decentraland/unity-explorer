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
    public partial class InitializeSceneLODInfo : BaseUnityLoopSystem
    {
        private readonly Transform lodParentTransform;

        //We cache the LODGroup and the byte of LODGroups loaded
        private readonly Dictionary<string, LODCacheInfo> lodGroupsCache;
        private readonly GameObjectPool<LODGroup> lodsGroupPool;

        public InitializeSceneLODInfo(World world, Transform lodParentTransform, GameObjectPool<LODGroup> lodsGroupPool) : base(world)
        {
            this.lodParentTransform = lodParentTransform;
            this.lodsGroupPool = lodsGroupPool;
            lodGroupsCache = new Dictionary<string, LODCacheInfo>();
        }

        protected override void Update(float t)
        {
            InitializeSceneLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InitializeSceneLOD(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //TODO (MISHA): Cache and initialization. Whats the best way?
            if (!string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            string sceneID = sceneDefinitionComponent.Definition.id;
            if (lodGroupsCache.TryGetValue(sceneID, out var lodCacheInfo))
            {
                var lodGroup = lodCacheInfo.LodGroup;
                lodGroup.gameObject.SetActive(true);
                sceneLODInfo.LodGroup = lodGroup;
                sceneLODInfo.LoadedLODs = lodCacheInfo.LoadedLODs;
                sceneLODInfo.CullRelativeHeight = lodCacheInfo.CullRelativeHeight;
            }
            else
            {
                //NOTE (Juani) : We need to initialize it every time. For the change of height trick to work,
                // the lod group should be active when modified
                sceneLODInfo.LodGroup = InitializeLODGroup(sceneID, lodParentTransform);
            }

            sceneLODInfo.id = sceneID;
            sceneLODInfo.lodGroupCache = lodGroupsCache;
            sceneLODInfo.lodGroupPool = lodsGroupPool;
        }

        private LODGroup InitializeLODGroup(string sceneID, Transform lodCacheParent)
        {
            var newLODGroup = lodsGroupPool.Get();
            newLODGroup.name = $"LODGroup_{sceneID}";
            newLODGroup.transform.SetParent(lodCacheParent);
            return newLODGroup;
        }
    }
}