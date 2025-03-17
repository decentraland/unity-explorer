using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Threading;
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
            var parcel =  world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
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
                    if (sceneFacade.Equals(targetScene))
                    {
                        sceneEntity = entity;
                    }
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
            await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Equals(SceneState.Disposed), cancellationToken: ct);

            if (localSceneDevelopment)
            {
                world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                    (ref StaticScenePointers staticScenePointers) =>
                    {
                        staticScenePointers.Promise = null;
                    });
            }
            else
            {
                // Forcing a fake IsDirty to force a reload of the scene at ResolveVisualSceneStateSystem.AddSceneVisualStateQuery()
                world.Get<PartitionComponent>(entity).IsDirty = true;
            }
        }
    }
}
