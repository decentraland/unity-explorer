using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle
{
    public class ECSReloadScene : IReloadScene
    {
        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;
        private readonly World world;

        public ECSReloadScene(IScenesCache scenesCache,
            World world,
            Entity playerEntity)
        {
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<bool> TryReloadSceneAsync(CancellationToken ct)
        {
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

            // TODO: inject...
            bool localSceneDevelopment = true;

            if (!localSceneDevelopment)
            {
                //Forcing a fake dirtyness to force a reload of the scene
                world.Get<PartitionComponent>(entity).IsDirty = true;
                return;
            }

            world.Query(in new QueryDescription().WithAll<RealmComponent>(),
                (ref ProcessedScenePointers processedPointers,
                    ref VolatileScenePointers volatileScenePointers,
                    ref RealmComponent realmComponent) =>
                {
                    foreach (Vector2Int parcel in currentScene.SceneData.Parcels)
                    {
                        processedPointers.Value.Remove(parcel.ToInt2());
                    }

                    var inputList = new List<int2>();
                    inputList.Add(currentScene.Info.BaseParcel.ToInt2());
                    volatileScenePointers.ActivePromise
                        = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                            new GetSceneDefinitionList(volatileScenePointers.RetrievedReusableList, inputList,
                                new CommonLoadingArguments(realmComponent.Ipfs.EntitiesActiveEndpoint)),
                            volatileScenePointers.ActivePartitionComponent);
                });
        }
    }
}
