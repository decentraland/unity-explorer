using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.SceneLifeCycle.CurrentScene;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Detects the scene the player is currently in
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class UpdateCurrentSceneSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly CurrentSceneInfo currentSceneInfo;

        private Vector2Int lastParcelProcessed;

        private readonly SceneAssetLock sceneAssetLock;

        private ElementBinding<string> sceneNameBinding;
        private ElementBinding<string> sceneParcelsBinding;
        private ElementBinding<string> sceneHeightBinding;

        internal UpdateCurrentSceneSystem(World world, IRealmData realmData, IScenesCache scenesCache, CurrentSceneInfo currentSceneInfo,
                                            Entity playerEntity, SceneAssetLock sceneAssetLock, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.playerEntity = playerEntity;
            this.sceneAssetLock = sceneAssetLock;
            ResetProcessedParcel();

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.CURRENT_SCENE)
                         .AddCustomMarker("Name:", sceneNameBinding = new ElementBinding<string>(string.Empty))
                         .AddCustomMarker("Parcels:", sceneParcelsBinding = new ElementBinding<string>(string.Empty))
                         .AddCustomMarker("Height (m):", sceneHeightBinding = new ElementBinding<string>(string .Empty));
        }

        private void ResetProcessedParcel()
        {
            lastParcelProcessed = new Vector2Int(int.MinValue, int.MinValue);
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                ResetProcessedParcel();
                return;
            }

            Vector3 playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            Vector2Int parcel = playerPos.ToParcel();
            UpdateSceneReadiness(parcel);
            UpdateCurrentScene(parcel);
            UpdateCurrentSceneInfo(parcel);

            RefreshSceneDebugInfo();

        }

        private void UpdateSceneReadiness(Vector2Int parcel)
        {
            if (!scenesCache.TryGetByParcel(parcel, out var currentScene))
                return;

            sceneAssetLock.TryLock(currentScene);

            if (!currentScene.SceneStateProvider.IsCurrent)
                currentScene.SetIsCurrent(true);
        }

        private void UpdateCurrentScene(Vector2Int parcel)
        {
            if (lastParcelProcessed == parcel) return;
            scenesCache.TryGetByParcel(lastParcelProcessed, out var lastProcessedScene);
            scenesCache.TryGetByParcel(parcel, out var currentScene);

            if (lastProcessedScene != currentScene)
                lastProcessedScene?.SetIsCurrent(false);

            if (currentScene is { SceneStateProvider: { IsCurrent: false } })
                currentScene.SetIsCurrent(true);

            lastParcelProcessed = parcel;
        }

        private void UpdateCurrentSceneInfo(Vector2Int parcel)
        {
            scenesCache.TryGetByParcel(parcel, out var currentScene);
            currentSceneInfo.Update(currentScene);
            scenesCache.SetCurrentScene(currentScene);
        }

        private void RefreshSceneDebugInfo()
        {
            if (scenesCache.CurrentScene != null)
            {
                if (sceneNameBinding.Value != scenesCache.CurrentScene.Info.Name)
                {
                    sceneNameBinding.Value = scenesCache.CurrentScene.Info.Name;

                    if (scenesCache.CurrentScene.SceneData.Parcels != null)
                    {
                        sceneParcelsBinding.Value = scenesCache.CurrentScene.SceneData.Parcels.Count.ToString();
                    }

                    sceneHeightBinding.Value = scenesCache.CurrentScene.SceneData.Geometry.Height.ToString();
                }
            }
            else
            {
                sceneNameBinding.Value = "<No data>";
                sceneParcelsBinding.Value = "<No data>";
                sceneHeightBinding.Value = "<No data>";
            }
        }
    }
}
