using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Based on formerly created intentions unloads the scene or interrupts its loading
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class UnloadSceneSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IScenesCache scenesCache;

        internal UnloadSceneSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UnloadLoadedSceneQuery(World);
            AbortLoadingScenesQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededScenesPromisesQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLoadedScene(in Entity entity, ref SceneDefinitionComponent definitionComponent, ref ISceneFacade sceneFacade)
        {
            // Keep definition so it won't be downloaded again = Cache in ECS itself
            sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache, definitionComponent.Parcels);
            World.Remove<ISceneFacade, VisualSceneState, DeleteEntityIntention>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, VisualSceneState, DeleteEntityIntention>(entity);
        }

        [Query]
        private void AbortSucceededScenesPromises(ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            if (!promise.IsConsumed && promise.TryConsume(World, out var result) && result.Succeeded)
                result.Asset!.DisposeAsync().Forget();
            else
                promise.ForgetLoading(World);
        }
    }
}
