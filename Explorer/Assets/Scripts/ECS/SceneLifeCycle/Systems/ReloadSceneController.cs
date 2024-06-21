using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Chat;
using DCL.DebugUtilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    public class ReloadSceneController
    {
        private IScenesCache scenesCache;
        private World world;
        public IChatMessagesBus chatMessagesBus;


        public void Initialize(IScenesCache scenesCache, World world, IDebugContainerBuilder debugBuilder)
        {
            this.scenesCache = scenesCache;
            this.world = world;

            debugBuilder.AddWidget("Scene Reload")
                .AddSingleButton("Reload Scene", () => chatMessagesBus.Send("/reload"));
        }

        public async UniTask<bool> TryReloadSceneAsync()
        {
            foreach (var sceneAnalyzed in scenesCache.Scenes)
            {
                if (sceneAnalyzed.SceneStateProvider.IsCurrent)
                {
                    var foundEntity = new Entity();
                    world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                        (Entity entity, ref ISceneFacade sceneFacade) =>
                        {
                            if (sceneFacade.Equals(sceneAnalyzed))
                                foundEntity = entity;
                        });
                    await DisposeAndRestartAsync(foundEntity, sceneAnalyzed);
                    return true;
                }
            }

            return false;
        }

        private async UniTask DisposeAndRestartAsync(Entity entity, ISceneFacade currentScene)
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