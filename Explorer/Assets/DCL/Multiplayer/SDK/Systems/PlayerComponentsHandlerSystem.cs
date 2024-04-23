using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using Utility;
using Avatar = DCL.Profiles.Avatar;

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
        [None(typeof(PlayerSDKDataComponent), typeof(DeleteEntityIntention))]
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

                // TODO: Optimize with a 'PlayerSDKDataComponent' pool??
                var playerSDKDataComponent = new PlayerSDKDataComponent
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
        private void UpdatePlayerSDKData(ref Profile profile, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            if (!profile.IsDirty) return;

            SceneEcsExecutor sceneEcsExecutor = playerSDKDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                Avatar avatarData = profile.Avatar;
                playerSDKDataComponent.IsDirty = true;
                playerSDKDataComponent.Address = profile.UserId;
                playerSDKDataComponent.IsGuest = !profile.HasConnectedWeb3;
                playerSDKDataComponent.Name = profile.Name;
                playerSDKDataComponent.BodyShapeURN = avatarData.BodyShape;
                playerSDKDataComponent.SkinColor = avatarData.SkinColor;
                playerSDKDataComponent.EyesColor = avatarData.EyesColor;
                playerSDKDataComponent.HairColor = avatarData.HairColor;
                playerSDKDataComponent.WearableUrns = profile.Avatar.Wearables;
                playerSDKDataComponent.EmoteUrns = profile.Avatar.Emotes;

                sceneEcsExecutor.World.Set(playerSDKDataComponent.SceneWorldEntity, playerSDKDataComponent);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandData(ref PlayerSDKDataComponent playerSDKData, ref CharacterEmoteIntent emoteIntent)
        {
            SceneEcsExecutor sceneEcsExecutor = playerSDKData.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                if (emoteCache.TryGetEmote(emoteIntent.EmoteId.Shorten(), out IEmote emote))
                {
                    playerSDKData.IsPlayingEmoteDirty = true;
                    playerSDKData.PreviousEmote = playerSDKData.PlayingEmote;
                    playerSDKData.PlayingEmote = emoteIntent.EmoteId;
                    playerSDKData.LoopingEmote = emote.IsLooping();

                    sceneEcsExecutor.World.Set(playerSDKData.SceneWorldEntity, playerSDKData);
                }
            }
        }

        [Query]
        private void RemovePlayerSDKDataOnOutsideCurrentScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            // Only target entities outside the current scene
            if (scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                && !sceneFacade.IsEmpty && sceneFacade.SceneStateProvider.IsCurrent) return;

            SceneEcsExecutor sceneEcsExecutor = playerSDKDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerSDKDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerSDKDataComponent>(playerSDKDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerSDKDataComponent.CRDTEntity.Id);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerDisconnect(in Entity entity, ref CharacterTransform characterTransform, PlayerSDKDataComponent playerSDKDataComponent)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                || sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            SceneEcsExecutor sceneEcsExecutor = playerSDKDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                World.Remove<PlayerSDKDataComponent>(entity);

                // Remove from whichever scene it was added
                sceneEcsExecutor.World.Remove<PlayerSDKDataComponent>(playerSDKDataComponent.SceneWorldEntity);
            }

            FreeReservedEntity(playerSDKDataComponent.CRDTEntity.Id);
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
