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

        private Entity playerEntity;
        private World? world;

        public ECSReloadScene(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public void Initialize(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<bool> TryReloadSceneAsync(CancellationToken ct)
        {
            if (world == null) return false;

            var parcel =  world.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();

            if (!scenesCache.TryGetByParcel(parcel, out var sceneInCache)) return false;

            var foundEntity = Entity.Null;

            world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                (Entity entity, ref ISceneFacade sceneFacade) =>
                {
                    if (sceneFacade.Equals(sceneInCache))
                    {
                        foundEntity = entity;
                    }
                });

            if (foundEntity == Entity.Null) return false;

            await DisposeAndRestartAsync(foundEntity, sceneInCache, ct);

            return true;
        }

        private async UniTask DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
            world!.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            world.Add<DeleteEntityIntention>(entity);

            //We wait until scene is fully disposed
            await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Equals(SceneState.Disposed), cancellationToken: ct);

            //Forcing a fake dirtyness to force a reload of the scene
            world.Get<PartitionComponent>(entity).IsDirty = true;
        }
    }
}
