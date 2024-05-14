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
using Utility;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    // Currently implemented to track reserved entities only on the CURRENT SCENE
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_CRDT_ENTITY)]
    public partial class PlayerCRDTEntitiesHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ICharacterObject mainPlayerCharacterObject;
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerCRDTEntitiesHandlerSystem(World world, IScenesCache scenesCache, ICharacterObject characterObject) : base(world)
        {
            this.scenesCache = scenesCache;
            mainPlayerCharacterObject = characterObject;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            RemoveComponentOnOutsideCurrentSceneQuery(World);

            RemoveComponentOnPlayerDisconnectQuery(World);

            RemoveComponentQuery(World);

            AddPlayerCRDTEntityQuery(World);
        }

        [Query]
        [All(typeof(Profile))]
        [None(typeof(PlayerCRDTEntity), typeof(DeleteEntityIntention))]
        private void AddPlayerCRDTEntity(in Entity entity, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            int crdtEntityId = characterTransform.Transform == mainPlayerCharacterObject.Transform ? SpecialEntitiesID.PLAYER_ENTITY : ReserveNextFreeEntity();

            // All reserved entities for that scene are taken
            if (crdtEntityId == -1) return;

            SceneEcsExecutor sceneEcsExecutor = sceneFacade.EcsExecutor;

            Entity sceneWorldEntity = sceneEcsExecutor.World.Create();
            var crdtEntity = new CRDTEntity(crdtEntityId);

            sceneEcsExecutor.World.Add(sceneWorldEntity, new PlayerCRDTEntity(crdtEntity, sceneFacade, sceneWorldEntity));

            World.Add(entity, new PlayerCRDTEntity(crdtEntity, sceneFacade, sceneWorldEntity));
        }

        [Query]
        private void RemoveComponentOnOutsideCurrentScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerCRDTEntity playerCRDTEntity)
        {
            // Only target entities outside the current scene
            if (scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                && !sceneFacade.IsEmpty && sceneFacade.SceneStateProvider.IsCurrent) return;

            RemoveComponent(entity, ref playerCRDTEntity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveComponentOnPlayerDisconnect(in Entity entity, ref CharacterTransform characterTransform, ref PlayerCRDTEntity playerCRDTEntity)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade)
                || sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            RemoveComponent(entity, ref playerCRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(Profile))]
        private void RemoveComponent(in Entity entity, ref PlayerCRDTEntity playerCRDTEntity)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            // Remove from whichever scene it was added. PlayerCRDTEntity is not removed here,
            // as the scene-level Writer systems need it to know which CRDT Entity to affect
            sceneEcsExecutor.World.Add<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity);

            FreeReservedEntity(playerCRDTEntity.CRDTEntity.Id);

            World.Remove<PlayerCRDTEntity>(entity);
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
