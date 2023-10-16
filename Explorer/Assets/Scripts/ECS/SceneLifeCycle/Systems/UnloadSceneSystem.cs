using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Realm;
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
            UnloadLoadedSceneQuery(World);
            AbortLoadingScenesQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLoadedScene(in Entity entity, ref ISceneFacade sceneFacade)
        {
            sceneFacade.DisposeAsync().Forget();

            // Keep definition so it won't be downloaded again = Cache in ECS itself
            World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>, DeleteEntityIntention>(entity);
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
