using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using DCL.Diagnostics;
using ECS.SceneLifeCycle.SceneDefinition;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem
    {
        private readonly ILODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;

        public UnloadSceneLODSystem(World world, ILODAssetsPool lodAssetsPool, IScenesCache scenesCache) : base(world)
        {
            this.lodAssetsPool = lodAssetsPool;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UnloadLODQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, World);
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}
