using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.Common;
using SceneRunner;
using SceneRunner.Scene;

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
            UnloadRunningSceneQuery(World);
            UnloadLoadedSceneQuery(World);
            AbortLoadingScenesQuery(World);
        }

        [Query]
        [All(typeof(UnloadRunningSceneIntention))]
        private void UnloadRunningScene(in Entity entity, ref ISceneFacade sceneFacade)
        {
            //TODO: We cannot dispose the scene, at the moment I will set targetFPS to 0
            if (!sceneFacade.IsDisposed)
            {
                sceneFacade.DisposeAsync().Forget();
                sceneFacade.IsDisposed = true;
            }
            //sceneFacade.SetTargetFPS(0);

            //TODO: We are leaving the scene facade so it can restart the scene until fully unloaded
            //World.Remove<ISceneFacade, UnloadRunningSceneIntention>(entity);
        }
        
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLoadedScene(in Entity entity, ref ISceneFacade sceneFacade, SceneLOD sceneLOD)
        {
            if (!sceneFacade.IsDisposed)
            {
                sceneFacade.DisposeAsync().Forget();
                sceneFacade.IsDisposed = true;
            }
            sceneLOD.Dispose();

            // Keep definition so it won't be downloaded again = Cache in ECS itself
            World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>, VisualSceneState,
                UnloadRunningSceneIntention, SceneLOD>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(ISceneFacade))]
        private void AbortLoadingScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, DeleteEntityIntention>(entity);
        }
    }
}
