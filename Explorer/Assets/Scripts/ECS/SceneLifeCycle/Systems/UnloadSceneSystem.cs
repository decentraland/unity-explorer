using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.Common;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Based on formerly created intentions unloads the scene or interrupts its loading
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class UnloadSceneSystem : BaseUnityLoopSystem
    {
        internal UnloadSceneSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UnloadLODQuery(World);
            UnloadLoadedSceneQuery(World);
            AbortLoadingScenesQuery(World);
        }
        
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.Dispose(World);
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }

        
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLoadedScene(in Entity entity, ref ISceneFacade sceneFacade)
        {
            // Keep definition so it won't be downloaded again = Cache in ECS itself
            sceneFacade.DisposeAsync().Forget();
            World.Remove<ISceneFacade, VisualSceneState, DeleteEntityIntention>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, VisualSceneState, DeleteEntityIntention>(
                entity);
        }
    }
}
