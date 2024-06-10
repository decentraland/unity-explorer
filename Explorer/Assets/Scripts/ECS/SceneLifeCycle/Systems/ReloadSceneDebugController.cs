using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class ReloadSceneDebugController 
    {
        private readonly Entity playerEntity;
        private readonly IScenesCache scenesCache;
        private readonly World world;

        public ReloadSceneDebugController(World world, Entity playerEntity, IScenesCache scenesCache,
            IDebugContainerBuilder debugBuilder)
        {
            this.playerEntity = playerEntity;
            this.scenesCache = scenesCache;
            this.world = world;

            debugBuilder.AddWidget("Scene Reload")
                .AddSingleButton("Reload Scene", ReloadScene);
        }

        private void ReloadScene()
        {
            var playerPos = world.Get<CharacterTransform>(playerEntity).Transform.position;
            var parcel = ParcelMathHelper.FloorToParcel(playerPos);
            if (scenesCache.TryGetByParcel(parcel, out var sceneInCache))
            {
                world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                    (Entity entity, ref ISceneFacade sceneFacade) =>
                    {
                        if (sceneFacade.Equals(sceneInCache))
                            DisposeAndRestartAsync(entity, sceneFacade);
                    });
            }
            else
            {
                ReportHub.Log(ReportCategory.REALM, "You need to be in an SDK7 scene to reload it");
            }
        }

        private async UniTaskVoid DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene)
        {
            //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
            world.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            world.Add<DeleteEntityIntention>(entity);

            //We wait until scene is fully disposed
            await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Equals(SceneState.Disposed));

            //Forcing a fake dirtyness to force a reload of the scene
            world.Get<PartitionComponent>(entity).IsDirty = true;
        }


    }
}