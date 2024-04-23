using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    // Currently implemented to track reserved entities only on the CURRENT SCENE
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_COMPONENTS_HANDLER)]
    public partial class PlayerComponentsHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ICharacterObject mainPlayerCharacterObject;
        private readonly IEmoteCache emoteCache;
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerComponentsHandlerSystem(World world, IScenesCache scenesCache, ICharacterObject characterObject, IEmoteCache emoteCache) : base(world)
        {
            this.scenesCache = scenesCache;
            this.emoteCache = emoteCache;
            mainPlayerCharacterObject = characterObject;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            RemovePlayerSDKDataOnOutsideCurrentSceneQuery(World);

            HandlePlayerDisconnectQuery(World);

            UpdatePlayerSDKDataQuery(World);
            UpdateEmoteCommandDataQuery(World);

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

                // TODO: Optimize with a 'PlayerProfileDataComponent' pool??
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
                    WearableUrns = profile.Avatar.Wearables,
                    EmoteUrns = profile.Avatar.Emotes,
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

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                Avatar avatarData = profile.Avatar;
                playerProfileDataComponent.IsDirty = true;
                playerProfileDataComponent.Address = profile.UserId;
                playerProfileDataComponent.IsGuest = !profile.HasConnectedWeb3;
                playerProfileDataComponent.Name = profile.Name;
                playerProfileDataComponent.BodyShapeURN = avatarData.BodyShape;
                playerProfileDataComponent.SkinColor = avatarData.SkinColor;
                playerProfileDataComponent.EyesColor = avatarData.EyesColor;
                playerProfileDataComponent.HairColor = avatarData.HairColor;
                playerProfileDataComponent.WearableUrns = profile.Avatar.Wearables;
                playerProfileDataComponent.EmoteUrns = profile.Avatar.Emotes;

                sceneEcsExecutor.World.Set(playerProfileDataComponent.SceneWorldEntity, playerProfileDataComponent);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandData(ref PlayerProfileDataComponent playerProfileData, ref CharacterEmoteIntent emoteIntent)
        {
            SceneEcsExecutor sceneEcsExecutor = playerProfileData.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                if (emoteCache.TryGetEmote(emoteIntent.EmoteId.Shorten(), out IEmote emote))
                {
                    playerProfileData.IsPlayingEmoteDirty = true;
                    playerProfileData.PreviousEmote = playerProfileData.PlayingEmote;
                    playerProfileData.PlayingEmote = emoteIntent.EmoteId;
                    playerProfileData.LoopingEmote = emote.IsLooping();

                    sceneEcsExecutor.World.Set(playerProfileData.SceneWorldEntity, playerProfileData);
                }
            }
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
