using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class ReloadSceneController
    {
        private Entity playerEntity;
        private IScenesCache scenesCache;
        private IDebugContainerBuilder debugBuilder;
        private World world;
        private IChatMessagesBus chatMessagesBus;

        public void Initialize(World world, Entity playerEntity, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;

            debugBuilder.AddWidget("Scene Reload")
                .AddSingleButton("Reload Scene", () =>  chatMessagesBus.Send("/reload"));
        }

        public void InitializeChatMessageBus(IChatMessagesBus containerChatMessagesBus)
        {
            chatMessagesBus = containerChatMessagesBus;
        }

        public async UniTask<bool> TryReloadSceneAsync()
        {
            var playerPos = world.Get<CharacterTransform>(playerEntity).Transform.position;
            var parcel = ParcelMathHelper.FloorToParcel(playerPos);
            if (scenesCache.TryGetByParcel(parcel, out var sceneInCache))
            {
                var foundEntity = Entity.Null;
                world.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                    (Entity entity, ref ISceneFacade sceneFacade) =>
                    {
                        if (sceneFacade.Equals(sceneInCache))
                        {
                            foundEntity = entity;
                        }
                    });
                if (foundEntity != Entity.Null)
                {
                    await DisposeAndRestartAsync(foundEntity, sceneInCache);
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