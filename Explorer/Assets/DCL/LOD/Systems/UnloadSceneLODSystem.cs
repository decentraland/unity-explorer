using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using System;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem
    {
        private readonly ILODAssetsPool lodAssetsPool;

        public UnloadSceneLODSystem(World world, ILODAssetsPool lodAssetsPool) : base(world)
        {
            this.lodAssetsPool = lodAssetsPool;
        }

        protected override void Update(float t)
        {
            UnloadLODQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.Dispose(World, lodAssetsPool);
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }
    }
}
