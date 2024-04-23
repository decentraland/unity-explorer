using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
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
using System.Collections.Generic;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    // Currently implemented to track reserved entities only on the CURRENT SCENE
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_PROFILE_DATA)]
    public partial class PlayerProfileDataPropagationSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ICharacterObject mainPlayerCharacterObject;
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerProfileDataPropagationSystem(World world, IScenesCache scenesCache, ICharacterObject characterObject) : base(world)
        {
            this.scenesCache = scenesCache;
            mainPlayerCharacterObject = characterObject;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            RemovePlayerSDKDataOnOutsideCurrentSceneQuery(World);

            HandlePlayerDisconnectQuery(World);

            UpdatePlayerSDKDataQuery(World);

            AddPlayerSDKDataQuery(World);
        }

        [Query]
        [None(typeof(PlayerProfileDataComponent), typeof(DeleteEntityIntention))]
        private void AddPlayerSDKData(in Entity entity, ref Profile profile, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            int crdtEntityId = characterTransform.Transform == mainPlayerCharacterObject.Transform ? SpecialEntitiesID.PLAYER_ENTITY : ReserveNextFreeEntity();

            // All reserved entities for that scene are taken
            if (crdtEntityId == -1) return;

            SceneEcsExecutor sceneEcsExecutor = sceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                Entity sceneWorldEntity = sceneEcsExecutor.World.Create();
                var crdtEntityComponent = new CRDTEntity(crdtEntityId);
                Avatar avatarData = profile.Avatar;

                var playerSDKDataComponent = new PlayerProfileDataComponent
                {
                    IsDirty = true,
                    SceneFacade = sceneFacade,
                    SceneWorldEntity = sceneWorldEntity,
                    CRDTEntity = crdtEntityComponent,
                    Address = profile.UserId,
                    IsGuest = !profile.HasConnectedWeb3,
                    Name = profile.Name,
                    BodyShapeURN = avatarData.BodyShape,
                    SkinColor = avatarData.SkinColor,
                    EyesColor = avatarData.EyesColor,
                    HairColor = avatarData.HairColor,
                    WearableUrns = new List<URN>(profile.Avatar.Wearables),
                    EmoteUrns = new List<URN>(profile.Avatar.Emotes),
                };

                sceneEcsExecutor.World.Add(sceneWorldEntity, playerSDKDataComponent);
                World.Add(entity, playerSDKDataComponent);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdatePlayerSDKData(ref Profile profile, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            if (!profile.IsDirty) return;

            SceneEcsExecutor sceneEcsExecutor = playerProfileDataComponent.SceneFacade.EcsExecutor;

            Avatar avatarData = profile.Avatar;
            playerProfileDataComponent.IsDirty = true;
            playerProfileDataComponent.Address = profile.UserId;
            playerProfileDataComponent.IsGuest = !profile.HasConnectedWeb3;
            playerProfileDataComponent.Name = profile.Name;
            playerProfileDataComponent.BodyShapeURN = avatarData.BodyShape;
            playerProfileDataComponent.SkinColor = avatarData.SkinColor;
            playerProfileDataComponent.EyesColor = avatarData.EyesColor;
            playerProfileDataComponent.HairColor = avatarData.HairColor;
            playerProfileDataComponent.WearableUrns = new List<URN>(profile.Avatar.Wearables);
            playerProfileDataComponent.EmoteUrns = new List<URN>(profile.Avatar.Emotes);

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
                sceneEcsExecutor.World.Set(playerProfileDataComponent.SceneWorldEntity, playerProfileDataComponent);
        }

        [Query]
        private void RemovePlayerSDKDataOnOutsideCurrentScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            // Only target entities outside the current scene
            if (scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                && !sceneFacade.IsEmpty && sceneFacade.SceneStateProvider.IsCurrent) return;

            SceneEcsExecutor sceneEcsExecutor = playerProfileDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerProfileDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerProfileDataComponent>(playerProfileDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerProfileDataComponent.CRDTEntity.Id);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerDisconnect(in Entity entity, ref CharacterTransform characterTransform, PlayerProfileDataComponent playerProfileDataComponent)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                || sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            SceneEcsExecutor sceneEcsExecutor = playerProfileDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerProfileDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerProfileDataComponent>(playerProfileDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerProfileDataComponent.CRDTEntity.Id);
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
