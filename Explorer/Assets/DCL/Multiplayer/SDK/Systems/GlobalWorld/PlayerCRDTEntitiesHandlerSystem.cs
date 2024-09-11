using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
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

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    // Currently implemented to track reserved entities only on the CURRENT SCENE
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerCRDTEntitiesHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly bool[] reservedEntities = new bool[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];
        private int currentReservedEntitiesCount;

        public PlayerCRDTEntitiesHandlerSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            RemoveComponentOnOutsideCurrentSceneQuery(World);

            RemoveComponentOnPlayerDisconnectQuery(World);

            RemoveComponentQuery(World);

            AddRemotePlayerCRDTEntityQuery(World);

            AddOwnPlayerCRDTEntityQuery(World);
        }

        [Query]
        [All(typeof(Profile))]
        [None(typeof(PlayerCRDTEntity), typeof(DeleteEntityIntention), typeof(PlayerComponent))]
        private void AddRemotePlayerCRDTEntity(in Entity entity, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(characterTransform.Transform.ParcelPosition(), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            int crdtEntityId = ReserveNextFreeEntity();

            // All reserved entities for that scene are taken
            if (crdtEntityId == -1) return;

            SceneEcsExecutor sceneEcsExecutor = sceneFacade.EcsExecutor;

            Entity sceneWorldEntity = sceneEcsExecutor.World.Create();
            var crdtEntity = new CRDTEntity(crdtEntityId);

            sceneEcsExecutor.World.Add(sceneWorldEntity, new PlayerSceneCRDTEntity(crdtEntity));

            World.Add(entity, new PlayerCRDTEntity(crdtEntity, sceneFacade, sceneWorldEntity));
        }

        [Query]
        [All(typeof(Profile), typeof(PlayerComponent))]
        [None(typeof(PlayerCRDTEntity), typeof(DeleteEntityIntention))]
        private void AddOwnPlayerCRDTEntity(in Entity entity, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(characterTransform.Transform.ParcelPosition(), out ISceneFacade sceneFacade))
                return;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            World.Add(entity, new PlayerCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY, sceneFacade, sceneFacade.PersistentEntities.Player, true));
        }

        [Query]
        private void RemoveComponentOnOutsideCurrentScene(in Entity entity, ref CharacterTransform characterTransform, ref PlayerCRDTEntity playerCRDTEntity)
        {
            // Only target entities outside the current scene
            if (scenesCache.TryGetByParcel(characterTransform.Transform.ParcelPosition(), out ISceneFacade sceneFacade)
                && !sceneFacade.IsEmpty && sceneFacade.SceneStateProvider.IsCurrent) return;

            RemoveComponent(entity, ref playerCRDTEntity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveComponentOnPlayerDisconnect(Entity entity, ref CharacterTransform characterTransform, ref PlayerCRDTEntity playerCRDTEntity)
        {
            if (!scenesCache.TryGetByParcel(characterTransform.Transform.ParcelPosition(), out ISceneFacade sceneFacade)
                || sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            RemoveComponent(entity, ref playerCRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(Profile))]
        private void RemoveComponent(Entity entity, ref PlayerCRDTEntity playerCRDTEntity)
        {
            if (!playerCRDTEntity.SceneEntityIsPersistent)
            {
                SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

                // Remove from whichever scene it was added. PlayerCRDTEntity is not removed here,
                // as the scene-level Writer systems need it to know which CRDT Entity to affect
                sceneEcsExecutor.World.Add<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity);

                FreeReservedEntity(playerCRDTEntity.CRDTEntity.Id);
            }

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

        private void FreeReservedEntity(int entityId)
        {
            entityId -= SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;
            if (entityId >= reservedEntities.Length || entityId < 0) return;

            reservedEntities[entityId] = false;
            currentReservedEntitiesCount--;
        }

        private void ClearReservedEntities()
        {
            for (var i = 0; i < reservedEntities.Length; i++) { reservedEntities[i] = false; }

            currentReservedEntitiesCount = 0;
        }
    }
}
