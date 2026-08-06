using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle
{
    public class ECSReloadScene
    {
        private const double DEFINITION_REFRESH_TIMEOUT_SECS = 3;

        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;
        private readonly World world;
        private readonly bool localSceneDevelopment;
        private readonly ICacheCleaner cacheCleaner;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public ECSReloadScene(IScenesCache scenesCache,
            World world,
            Entity playerEntity,
            bool localSceneDevelopment,
            ICacheCleaner cacheCleaner,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localSceneDevelopment = localSceneDevelopment;
            this.cacheCleaner = cacheCleaner;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct)
        {
            var parcel = world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache, ct);

            return sceneInCache;
        }

        public async UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct, string sceneId)
        {
            if (!scenesCache.TryGetBySceneId(sceneId, out var sceneInCache)) return null;

            var foundEntity = FindSceneEntity(sceneInCache!);
            if (foundEntity == Entity.Null) return null;

            await DisposeAndRestartAsync(foundEntity, sceneInCache!, ct);

            return sceneInCache;
        }

        private Entity FindSceneEntity(ISceneFacade targetScene)
        {
            var sceneEntity = Entity.Null;

            world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                (Entity entity, ref ISceneFacade sceneFacade) =>
                {
                    if (sceneFacade.Equals(targetScene)) { sceneEntity = entity; }
                });

            return sceneEntity;
        }

        private async UniTask DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Gate facade promise recreation on the kept definition entity: UnloadSceneSystem strips
            // the facade components as soon as the unload starts, and without the gate
            // ResolveStaticPointersSystem would recreate the promise before the refreshed
            // definition lands.
            if (localSceneDevelopment)
                world.Add<SceneDefinitionRefreshPending>(entity);

            try
            {
                // The definition's content list changes when files are added, removed or renamed
                // (in local scene development the file path is the content hash). Fetch the fresh
                // definition in parallel so it lands within the dispose wait below.
                UniTask<SceneEntityDefinition?> definitionRefresh = localSceneDevelopment
                    ? FetchRefreshedDefinitionAsync(entity, ct)
                    : UniTask.FromResult<SceneEntityDefinition?>(null);

                //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
                world.Add<DeleteEntityIntention>(entity);

                //We wait until scene is fully disposed
                await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Value() == SceneState.Disposed, cancellationToken: ct);

                if (world.IsAlive(entity))
                {
                    SceneLoadingState sceneLoadingState = world.Get<SceneLoadingState>(entity);
                    sceneLoadingState.VisualSceneState = VisualSceneState.Uninitialized;
                    sceneLoadingState.PromiseCreated = false;
                }

                if (!localSceneDevelopment) return;

                // Force-drain dereferenced caches on LSD reload. The local dev server derives hashes
                // from the file path, not content, so an updated model keeps the same hash and cache
                // hits would return stale assets. Draining guarantees fresh loads.
                cacheCleaner.UnloadCache(budgeted: false);
                Resources.UnloadUnusedAssets();

                ApplyRefreshedDefinition(entity, await definitionRefresh);
            }
            finally
            {
                if (localSceneDevelopment && world.IsAlive(entity))
                    world.Remove<SceneDefinitionRefreshPending>(entity);
            }

            await WaitUntilNewSceneIsFullyLoadedAsync();

            return;

            async UniTask WaitUntilNewSceneIsFullyLoadedAsync()
            {
                await UniTask.WaitUntil(() =>
                {
                    var isLoadCompleted = false;

                    // TODO: filter by scene coord/id? We currently assume that only one scene will be running during local scene development
                    world.Query(in new QueryDescription().WithAll<ISceneFacade>().WithNone<DeleteEntityIntention>(),
                        (ref ISceneFacade newScene) =>
                        {
                            if (newScene.SceneStateProvider.State.Value() is SceneState.JavaScriptError
                                or SceneState.EcsError)
                            {
                                isLoadCompleted = true;
                                return;
                            }

                            isLoadCompleted = newScene.SceneStateProvider.State.Value() is SceneState.Running
                                              // Consider GLTF models in the initial loading phase since they're not tracked by SceneStateProvider.State.
                                              // This prevents the character from falling through unloaded colliders during scene reload.
                                              && newScene.SceneData.SceneLoadingConcluded;
                        });

                    return isLoadCompleted;
                }, cancellationToken: ct);
            }
        }

        private async UniTask<SceneEntityDefinition?> FetchRefreshedDefinitionAsync(Entity entity, CancellationToken ct)
        {
            Vector2Int baseParcel = world.Get<SceneDefinitionComponent>(entity).Definition.metadata.scene.DecodedBase;

            try
            {
                SceneEntityDefinition[] definitions = await webRequestController
                                                           .PostAsync(new CommonArguments(URLAddress.FromString(urlsSource.Url(DecentralandUrl.EntitiesActive))),
                                                                GenericPostArguments.CreateJson($"{{\"pointers\":[\"{baseParcel.x},{baseParcel.y}\"]}}"),
                                                                ct, ReportCategory.SCENE_LOADING)
                                                           .CreateFromJson<SceneEntityDefinition[]>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool)
                                                           .Timeout(TimeSpan.FromSeconds(DEFINITION_REFRESH_TIMEOUT_SECS));

                await UniTask.SwitchToMainThread(ct);

                return definitions.Length > 0 ? definitions[0] : null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread(ct);
                ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"Scene reload: definition refresh failed, reusing the cached definition: {e.Message}");
                return null;
            }
        }

        internal void ApplyRefreshedDefinition(Entity entity, SceneEntityDefinition? refreshed)
        {
            if (refreshed == null || !world.IsAlive(entity)) return;

            SceneEntityDefinition cached = world.Get<SceneDefinitionComponent>(entity).Definition;

            // A parcel layout change invalidates the precomputed scene geometry on the kept
            // component: fall back to full re-discovery, which re-creates the definition entity.
            if (!PointersEqual(cached.pointers, refreshed.pointers))
            {
                world.Destroy(entity);

                world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                    (ref StaticScenePointers staticScenePointers) => { staticScenePointers.Promise = null; });

                return;
            }

            // Same parcels: adopt the fresh content list so files added, removed or renamed since
            // the definition was cached resolve correctly on this reload.
            cached.content = refreshed.content;
        }

        private static bool PointersEqual(string[] cached, string[] refreshed)
        {
            if (cached.Length != refreshed.Length) return false;

            for (var i = 0; i < cached.Length; i++)
            {
                if (cached[i] != refreshed[i])
                    return false;
            }

            return true;
        }
    }
}
