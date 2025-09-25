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
using Utility;

namespace ECS.SceneLifeCycle
{
    public class ECSBannedScene
    {
        private readonly IScenesCache scenesCache;
        private readonly World world;
        private readonly Entity playerEntity;

        public ECSBannedScene(IScenesCache scenesCache, World world, Entity playerEntity)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<bool> TrySetCurrentSceneAsBannedAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            var parcel = world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache))
                return false;

            var foundEntity = FindSceneEntity(sceneInCache);
            if (foundEntity == Entity.Null)
                return false;

            world.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(foundEntity);
            world.Add<DeleteEntityIntention>(foundEntity);
            world.Add<BannedSceneComponent>(foundEntity);

            await UniTask.WaitUntil(() => sceneInCache.SceneStateProvider.State.Value() == SceneState.Disposed, cancellationToken: ct);

            if (world.IsAlive(foundEntity))
            {
                SceneLoadingState sceneLoadingState = world.Get<SceneLoadingState>(foundEntity);
                sceneLoadingState.VisualSceneState = VisualSceneState.UNINITIALIZED;
                sceneLoadingState.PromiseCreated = false;
            }

            return true;
        }

        public void RemoveAllBannedSceneComponents() =>
            world.Query(in new QueryDescription().WithAll<BannedSceneComponent>(), entity => world.Remove<BannedSceneComponent>(entity));

        private Entity FindSceneEntity(ISceneFacade targetScene)
        {
            var sceneEntity = Entity.Null;

            world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                (Entity entity, ref ISceneFacade sceneFacade) =>
                {
                    if (sceneFacade.Equals(targetScene))
                        sceneEntity = entity;
                });

            return sceneEntity;
        }
    }
}
