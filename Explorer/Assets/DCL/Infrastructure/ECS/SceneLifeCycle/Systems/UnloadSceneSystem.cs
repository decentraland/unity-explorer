using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Profiling;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
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
        private readonly bool localSceneDevelopment;

        internal UnloadSceneSystem(World world, IScenesCache scenesCache, bool localSceneDevelopment) : base(world)
        {
            this.scenesCache = scenesCache;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        protected override void Update(float t)
        {
            using (ProfilerSampleScope.New(nameof(UnloadLoadedSceneQuery)))
                UnloadLoadedSceneQuery(World);

            using (ProfilerSampleScope.New(nameof(UnloadLoadedPortableExperienceSceneQuery)))
                UnloadLoadedPortableExperienceSceneQuery(World);

            using (ProfilerSampleScope.New(nameof(CleanSceneFacadeWhenLODQuery)))
                CleanSceneFacadeWhenLODQuery(World);

            using (ProfilerSampleScope.New(nameof(CleanScenePromiseWhenLODQuery)))
                CleanScenePromiseWhenLODQuery(World);

            using (ProfilerSampleScope.New(nameof(AbortLoadingScenesQuery)))
                AbortLoadingScenesQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededScenesPromisesQuery(World);
        }

        [Query]
        [All(typeof(SceneLODInfo))]
        private void CleanSceneFacadeWhenLOD(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref ISceneFacade sceneFacade, ref SceneLoadingState sceneLoadingState)
        {
            using var _ = ProfilerSampleScope.New(sceneDefinitionComponent.Definition.GetLogSceneName());
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_LOD)
            {
                //TODO: Wait until LOD is Ready
                //Dispose scene
                sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache,
                    sceneDefinitionComponent.Parcels);

                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        [All(typeof(SceneLODInfo))]
        private void CleanScenePromiseWhenLOD(in Entity entity,
            ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref SceneLoadingState sceneLoadingState)
        {
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_LOD)
            {
                //TODO: Wait until LOD is Ready
                //Dispose scene
                promise.ForgetLoading(World);
                World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(PortableExperienceComponent))]
        private void UnloadLoadedScene(in Entity entity, ref SceneDefinitionComponent definitionComponent, ref ISceneFacade sceneFacade)
        {
            sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache, definitionComponent.Parcels);
            ReportHub.LogProductionInfo($"Scene '{definitionComponent.Definition?.GetLogSceneName()}' disposed");

            // Keep definition so it won't be downloaded again = Cache in ECS itself
            if (!localSceneDevelopment)
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>, DeleteEntityIntention>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), (typeof(PortableExperienceComponent)))]
        private void UnloadLoadedPortableExperienceScene(in Entity entity, ref SceneDefinitionComponent definitionComponent, ref ISceneFacade sceneFacade)
        {
            sceneFacade.DisposeAsync().Forget();
            scenesCache.RemovePortableExperienceFacade(definitionComponent.IpfsPath.EntityId);
            World.Destroy(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, DeleteEntityIntention>(entity);
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
