using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle
{
    public class ECSReloadScene : IReloadScene
    {
        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;
        private readonly World world;
        private readonly bool localSceneDevelopment;

        public ECSReloadScene(IScenesCache scenesCache,
            World world,
            Entity playerEntity,
            bool localSceneDevelopment)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public async UniTask<bool> TryReloadSceneAsync(CancellationToken ct)
        {
            var parcel = world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache)) return false;

            var foundEntity = FindSceneEntity(sceneInCache);
            if (foundEntity == Entity.Null) return false;

            await DisposeAndRestartAsync(foundEntity, sceneInCache, ct);

            return true;
        }

        public async UniTask<bool> TryReloadSceneAsync(CancellationToken ct, string sceneId)
        {
            if (!scenesCache.TryGetBySceneId(sceneId, out var sceneInCache)) return false;

            var foundEntity = FindSceneEntity(sceneInCache!);
            if (foundEntity == Entity.Null) return false;

            await DisposeAndRestartAsync(foundEntity, sceneInCache!, ct);

            return true;
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

            //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
            world!.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            world.Add<DeleteEntityIntention>(entity);

            //We wait until scene is fully disposed
            await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Value() == SceneState.Disposed, cancellationToken: ct);

            if (world.IsAlive(entity))
            {
                SceneLoadingState sceneLoadingState = world.Get<SceneLoadingState>(entity);
                sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
                sceneLoadingState.PromiseCreated = false;
            }

            if (localSceneDevelopment)
            {
                world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                    (ref StaticScenePointers staticScenePointers) => { staticScenePointers.Promise = null; });

                Resources.UnloadUnusedAssets();

                await WaitUntilNewSceneIsFullyLoadedAsync();
            }

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
    }
}
