using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.SDK.Systems
{
    // Currently implemented to track reserved entities only on the CURRENT SCENE
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_COMPONENTS_HANDLER)]
    public partial class PlayerComponentsHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ICharacterObject mainPlayerCharacterObject;
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerComponentsHandlerSystem(World world, IScenesCache scenesCache, ICharacterObject characterObject) : base(world)
        {
            this.scenesCache = scenesCache;
            mainPlayerCharacterObject = characterObject;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            RemovePlayerIdentityDataOnOutsideCurrentSceneQuery(World);

            HandlePlayerDisconnectQuery(World);

            AddPlayerIdentityDataQuery(World);
        }

        [Query]
        [None(typeof(PlayerIdentityDataComponent), typeof(DeleteEntityIntention))]
        private void AddPlayerIdentityData(in Entity entity, ref Profile profile, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.SceneStateProvider == null || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            int crdtEntityId = characterTransform.Transform == mainPlayerCharacterObject.Transform ? SpecialEntitiesID.PLAYER_ENTITY : ReserveNextFreeEntity();

            // All reserved entities for that scene are taken
            if (crdtEntityId == -1) return;

            SceneEcsExecutor sceneEcsExecutor = sceneFacade.EcsExecutor!.Value;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                Entity sceneWorldEntity = sceneEcsExecutor.World.Create();
                var crdtEntityComponent = new CRDTEntity(crdtEntityId);

                var playerIdentityData = new PlayerIdentityDataComponent
                {
                    SceneFacade = sceneFacade,
                    SceneWorldEntity = sceneWorldEntity,
                    CRDTEntity = crdtEntityComponent,
                    Address = profile.UserId,
                    IsGuest = !profile.HasConnectedWeb3, // TODO: check if this assumption is correct
                };

                sceneEcsExecutor.World.Add(sceneWorldEntity, playerIdentityData);
                World.Add(entity, playerIdentityData);
            }
        }

        [Query]
        private void RemovePlayerIdentityDataOnOutsideCurrentScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            // Only target entities outside the current scene
            if (scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                && sceneFacade.SceneStateProvider != null && sceneFacade.SceneStateProvider.IsCurrent) return;

            SceneEcsExecutor sceneEcsExecutor = playerIdentityDataComponent.SceneFacade.EcsExecutor!.Value;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerIdentityDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerIdentityDataComponent>(playerIdentityDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerIdentityDataComponent.CRDTEntity.Id);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerDisconnect(in Entity entity, ref CharacterTransform characterTransform, PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            SceneEcsExecutor sceneEcsExecutor = playerIdentityDataComponent.SceneFacade.EcsExecutor!.Value;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerIdentityDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerIdentityDataComponent>(playerIdentityDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerIdentityDataComponent.CRDTEntity.Id);
        }

        private int ReserveNextFreeEntity()
        {
            // All reserved entities are taken
            if (currentReservedEntitiesCount == reservedEntities.Length)
                return -1;

            for (var i = 0; i < reservedEntities.Length; i++)
            {
                if (!reservedEntities[i])
                {
                    reservedEntities[i] = true;
                    currentReservedEntitiesCount++;
                    return SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + i;
                }
            }

            return -1;
        }

        public void FreeReservedEntity(int entityId)
        {
            entityId -= SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;
            if (entityId >= reservedEntities.Length || entityId < 0) return;

            reservedEntities[entityId] = false;
            currentReservedEntitiesCount--;
        }

        public void ClearReservedEntities()
        {
            for (var i = 0; i < reservedEntities.Length; i++) { reservedEntities[i] = false; }

            currentReservedEntitiesCount = 0;
        }
    }
}
