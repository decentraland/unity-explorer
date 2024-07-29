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
        private readonly ILODCache lodCache;
        private readonly GameObjectPool<LODGroup> lodsGroupPool;
        private readonly int lodLevels;


        public InitializeSceneLODInfo(World world, Transform lodParentTransform, GameObjectPool<LODGroup> lodsGroupPool, ILODCache lodCache, int lodLevels) : base(world)
        {
            this.lodLevels = lodLevels;
            this.lodParentTransform = lodParentTransform;
            this.lodsGroupPool = lodsGroupPool;
            this.lodCache = lodCache;
        }

        protected override void Update(float t)
        {
            InitializeSceneLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InitializeSceneLOD(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //TODO (MISHA): Initialization. Whats the best way?
            if (!string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            string sceneID = sceneDefinitionComponent.Definition.id;
            if (lodCache.TryGet(sceneID, out var lodCacheInfo))
                sceneLODInfo.metadata = lodCacheInfo;
            else
            {
                //NOTE (Juani) : We need to initialize it every time. For the change of height trick to work,
                // the lod group should be active when modified
                var lodGroup = InitializeLODGroup(sceneID, lodParentTransform);
                sceneLODInfo.metadata = new LODCacheInfo
                {
                    LodGroup = lodGroup, LODAssets = new LODAsset[lodLevels]
                };
            }

            sceneLODInfo.id = sceneID;
            sceneLODInfo.lodCache = lodCache;
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