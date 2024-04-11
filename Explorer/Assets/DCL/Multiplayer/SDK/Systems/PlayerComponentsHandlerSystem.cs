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
        private readonly ICharacterObject characterObject;

        // TODO: Clear when current scene changes before populating the new one
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerComponentsHandlerSystem(World world, IScenesCache scenesCache, ICharacterObject characterObject) : base(world)
        {
            this.scenesCache = scenesCache;
            this.characterObject = characterObject;
        }

        protected override void Update(float t)
        {
            UpdatePlayerIdentityDataQuery(World);
            UpdatePlayerEntitiesThatLeftSceneQuery(World);
        }

        [Query]
        [None(typeof(PlayerIdentityDataComponent))]
        private void UpdatePlayerIdentityData(in Entity entity, ref Profile profile, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (!sceneFacade.SceneStateProvider.IsCurrent) return;

            int crdtEntityId = ReserveNextFreeEntity();

            // All reserved entities for that scene are taken
            if (crdtEntityId == -1) return;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneFacade.EcsExecutor.Sync.GetScope())
            {
                var crdtEntityComponent = new CRDTEntity(crdtEntityId);
                Debug.Log($"PRAVS - UpdatePlayerIdentityDataQuery() - Entity: {entity.Id}; CRDTEntity: {crdtEntityComponent.Id}; Address: {profile.UserId}; scene parcel: {sceneFacade.Info.BaseParcel}");

                var playerIdentityData = new PlayerIdentityDataComponent
                {
                    CRDTEntity = crdtEntityComponent,
                    Address = profile.UserId,
                    IsGuest = !profile.HasConnectedWeb3, // TODO: check if this assumption is correct
                };

                World.Add(entity, playerIdentityData);

                // sceneFacade.EcsExecutor.World.Add(entity, playerIdentityData, crdtEntityComponent);
                sceneFacade.EcsExecutor.World.Add(entity, playerIdentityData);
            }
        }

        [Query]
        private void UpdatePlayerEntitiesThatLeftScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.SceneStateProvider.IsCurrent) return;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneFacade.EcsExecutor.Sync.GetScope())
            {
                Debug.Log($"PRAVS - UpdatePlayerEntitiesThatLeftTheScene() - Entity: {entity.Id}; CRDTEntity: {playerIdentityDataComponent.CRDTEntity.Id}; scene parcel: {sceneFacade.Info.BaseParcel}");

                World.Remove<PlayerIdentityDataComponent>(entity);

                // Remove from current scene (not that player's scene) entities
                scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterObject.Transform.position), out ISceneFacade currentSceneFacade);
                currentSceneFacade.EcsExecutor.World.Remove<PlayerIdentityDataComponent>(entity);
            }

            FreeEntity(playerIdentityDataComponent.CRDTEntity.Id);
        }

        // TODO: Optimize
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

        public void FreeEntity(int entityId)
        {
            entityId -= SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;
            if (entityId >= reservedEntities.Length) return;

            reservedEntities[entityId] = false;
            currentReservedEntitiesCount--;
        }

        public void Clear()
        {
            for (var i = 0; i < reservedEntities.Length; i++) { reservedEntities[i] = false; }

            currentReservedEntitiesCount = 0;
        }
    }
}
