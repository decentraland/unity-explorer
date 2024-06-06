using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
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
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class ReloadSceneDebugSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private Vector2Int lastParcelProcessed;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private ISceneFacade currentScene;

        private readonly ElementBinding<string> isSDK7;
        private bool currentSceneIsSDK7;

        public ReloadSceneDebugSystem(World world, IRealmData realmData, Entity playerEntity, IScenesCache scenesCache,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.scenesCache = scenesCache;
            isSDK7 = new ElementBinding<string>("Loading...");

            debugBuilder.AddWidget("Scene Reload")
                .AddCustomMarker("Is SDK7", isSDK7)
                .AddSingleButton("Reload Scene", ReloadScene);

            ResetProcessedParcel();
        }

        private void ReloadScene()
        {
            if (!currentSceneIsSDK7) return;

            World.Query(in new QueryDescription().WithAll<ISceneFacade, SceneDefinitionComponent>(),
                (Entity entity, ref ISceneFacade sceneFacade) =>
                {
                    if (sceneFacade.Equals(currentScene))
                        DisposeAndRestartAsync(entity);
                });
        }

        private async UniTaskVoid DisposeAndRestartAsync(Entity entity)
        {
            //There is a lingering promise we need to remove, and add the DeleteEntityIntention to make the standard unload flow.
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            World.Add<DeleteEntityIntention>(entity);

            //We wait until scene is fully disposed
            await UniTask.WaitUntil(() => currentScene.SceneStateProvider.State.Equals(SceneState.Disposed));

            //Forcing a fake dirtyness to force a reload of the scene
            World.Get<PartitionComponent>(entity).IsDirty = true;

            ResetProcessedParcel();

            //We need to keep resetting the processed parcel until it has been fully reloaded
            while (scenesCache.TryGetByParcel(lastParcelProcessed, out var sceneInCache))
            {
                ResetProcessedParcel();
                await UniTask.Yield();
            }
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                ResetProcessedParcel();
                return;
            }

            var playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            var parcel = ParcelMathHelper.FloorToParcel(playerPos);
            UpdateCurrentScene(parcel);
        }

        private void ResetProcessedParcel()
        {
            lastParcelProcessed = new Vector2Int(int.MinValue, int.MinValue);
        }

        private void UpdateCurrentScene(Vector2Int parcel)
        {
            //if (lastParcelProcessed == parcel) return;

            if (scenesCache.TryGetByParcel(parcel, out var sceneInCache))
            {
                currentScene = sceneInCache;
                currentSceneIsSDK7 = true;
                isSDK7.SetAndUpdate("YES");
            }
            else
            {
                currentSceneIsSDK7 = false;
                isSDK7.SetAndUpdate("NO");
            }


            lastParcelProcessed = parcel;
        }
    }
}