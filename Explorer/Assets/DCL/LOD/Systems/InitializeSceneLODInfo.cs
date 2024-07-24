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
        private readonly GameObjectPool<LODGroup> lodGroupPool;
        private readonly Transform lodParentTransform;

        //We cache the LODGroup and the byte of LODGroups loaded
        private readonly Dictionary<string, (LODGroup, byte, float)> lodGroupsCache;

        public InitializeSceneLODInfo(World world, GameObjectPool<LODGroup> lodGroupPool, Transform lodParentTransform) : base(world)
        {
            this.lodGroupPool = lodGroupPool;
            this.lodParentTransform = lodParentTransform;
            lodGroupsCache = new Dictionary<string, (LODGroup, byte, float)>();
        }

        protected override void Update(float t)
        {
            InitializeSceneLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InitializeSceneLOD(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.State == SCENE_LOD_INFO_STATE.UNINITIALIZED)
            {
                //TODO (JUANI) : How to cache failed lods? Should the hold SceneLODInfo be cached?
                if (lodGroupsCache.TryGetValue(sceneDefinitionComponent.Definition.id, out var lodCache))
                {
                    var lodGroup = lodCache.Item1;
                    lodGroup.gameObject.SetActive(true);
                    sceneLODInfo.LodGroup = lodGroup;
                    sceneLODInfo.LoadedLODs = lodCache.Item2;
                    sceneLODInfo.CullRelativeHeight = lodCache.Item3;
                    sceneLODInfo.State = SCENE_LOD_INFO_STATE.SUCCESS;
                }
                else
                {
                    var newLODGroup = lodGroupPool.Get();
                    newLODGroup.transform.SetParent(lodParentTransform);
                    newLODGroup.name = $"LODGroup_{sceneDefinitionComponent.Definition.id}";
                    sceneLODInfo.LodGroup = newLODGroup;
                    sceneLODInfo.State = SCENE_LOD_INFO_STATE.WAITING_LOD;
                }

                sceneLODInfo.id = sceneDefinitionComponent.Definition.id;
                sceneLODInfo.lodGroupCache = lodGroupsCache;
                sceneLODInfo.lodGroupPool = lodGroupPool;
            }
        }
    }
}