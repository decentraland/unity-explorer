using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
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
        private readonly IScenesCache scenesCache;

        internal UnloadSceneSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UnloadLoadedSceneQuery(World);
            AbortLoadingScenesQuery(World);
            AbortLoadingScenes2Query(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLoadedScene(in Entity entity, ref SceneDefinitionComponent definitionComponent, ref ISceneFacade sceneFacade)
        {
            // Keep definition so it won't be downloaded again = Cache in ECS itself
            Debug.Log($"VVV DISPOSE scene FACADE {sceneFacade.Info.BaseParcel} - {sceneFacade.Info.Name}, on entity {entity.Id}");
            sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache, definitionComponent.Parcels);
            World.Remove<ISceneFacade, VisualSceneState>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            Debug.Log($"VVV FORGOT scene PROMISE loading {promise.Entity.Entity.Id} - {promise.LoadingIntention.DefinitionComponent.Definition.metadata.scene.DecodedBase}");

            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, VisualSceneState>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes2(in Entity entity, ref GetSceneFacadeIntention intention)
        {
            Debug.Log($"VVV CANCEL scene loading {intention.DefinitionComponent.Definition.metadata.scene.DecodedBase}");
            intention.CancellationTokenSource.Cancel();
            World.Remove<GetSceneFacadeIntention>(entity);
        }
    }
}
