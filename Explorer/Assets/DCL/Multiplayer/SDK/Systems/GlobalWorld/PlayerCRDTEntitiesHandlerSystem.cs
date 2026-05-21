using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
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
            RemoveComponentOnPlayerDisconnectQuery(World);

            RemoveComponentQuery(World);

            ModifyPlayerSceneQuery(World);

            AddPlayerCRDTEntityQuery(World);
        }

        [Query]
        [All(typeof(Profile))]
        [None(typeof(PlayerCRDTEntity), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void AddPlayerCRDTEntity(Entity entity, in CharacterTransform characterTransform)
        {
            // Reserve entity straight-away, numeration will be preserved across all scenes
            int crdtEntityId = World.Has<PlayerComponent>(entity) ? SpecialEntitiesID.PLAYER_ENTITY : ReserveNextFreeEntity();

            // All reserved entities are taken
            if (crdtEntityId == -1) return;

            var playerCRDTEntity = new PlayerCRDTEntity(crdtEntityId);

            ResolvePlayerCRDTScene(characterTransform, ref playerCRDTEntity, playerCRDTEntity.CRDTEntity);

            World.Add(entity, playerCRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarShape))]
        private void ModifyPlayerScene(in CharacterTransform characterTransform, ref PlayerCRDTEntity playerCRDTEntity)
        {
            ResolvePlayerCRDTScene(characterTransform, ref playerCRDTEntity, playerCRDTEntity.CRDTEntity);
        }

        private void ResolvePlayerCRDTScene(in CharacterTransform characterTransform, ref PlayerCRDTEntity globalPlayerCRDTEntity, CRDTEntity reservedEntityId)
        {
            bool newSceneIsValid = scenesCache.TryGetByParcel(characterTransform.Transform.ParcelPosition(), out ISceneFacade currentScene)
                                   && currentScene.SceneStateProvider.State.Value() is SceneState.Running or SceneState.Starting
                                   && !currentScene.IsEmpty;

            if (globalPlayerCRDTEntity.SceneFacade != currentScene)
            {
                if (globalPlayerCRDTEntity.SceneWorldEntity != Entity.Null
                    && globalPlayerCRDTEntity.SceneFacade is not null)
                {
                    RemovePlayerFromScene(globalPlayerCRDTEntity.SceneWorldEntity, reservedEntityId, globalPlayerCRDTEntity.SceneFacade);
                }

                if (newSceneIsValid)
                {
                    SceneEcsExecutor sceneEcsExecutor = currentScene.EcsExecutor;
                    Entity sceneWorldEntity;

                    if (reservedEntityId.Id == SpecialEntitiesID.PLAYER_ENTITY)
                    {
                        sceneWorldEntity = currentScene.PersistentEntities.Player;

                        if (!sceneEcsExecutor.World.Has<PlayerSceneCRDTEntity>(sceneWorldEntity))
                            sceneEcsExecutor.World.Add(sceneWorldEntity, new PlayerSceneCRDTEntity(reservedEntityId));
                    }
                    else
                    {
                        sceneWorldEntity = sceneEcsExecutor.World.Create();
                        sceneEcsExecutor.World.Add(sceneWorldEntity, new PlayerSceneCRDTEntity(reservedEntityId));
                    }

                    globalPlayerCRDTEntity.AssignToScene(currentScene, sceneWorldEntity);
                }
                else { globalPlayerCRDTEntity.RemoveFromScene(); }
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(PlayerComponent))] // Host can't disconnect
        private void RemoveComponentOnPlayerDisconnect(Entity entity, ref PlayerCRDTEntity playerCRDTEntity)
        {
            RemoveComponent(entity, ref playerCRDTEntity, true);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(Profile))]
        private void RemoveComponent(Entity entity, ref PlayerCRDTEntity playerCRDTEntity)
        {
            RemoveComponent(entity, ref playerCRDTEntity, true);
        }

        private void RemoveComponent(Entity entity, ref PlayerCRDTEntity playerCRDTEntity, bool noLongerExists)
        {
            if (playerCRDTEntity is { AssignedToScene: true, SceneFacade: not null })
            {
                if (playerCRDTEntity.SceneWorldEntity != Entity.Null)
                {
                    RemovePlayerFromScene(playerCRDTEntity.SceneWorldEntity, playerCRDTEntity.CRDTEntity, playerCRDTEntity.SceneFacade);
                }

                if (noLongerExists)
                    FreeReservedEntity(playerCRDTEntity.CRDTEntity.Id);
            }

            if (noLongerExists)
                World.Remove<PlayerCRDTEntity>(entity);
        }

        private static void RemovePlayerFromScene(Entity sceneWorldEntity, CRDTEntity crdtEntity, ISceneFacade sceneFacade)
        {
            bool isLocalPlayer = crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY;

            SceneState state = sceneFacade.SceneStateProvider.State.Value();

            if (state != SceneState.Running && state != SceneState.Starting)
                return;

            SceneEcsExecutor executor = sceneFacade.EcsExecutor;

            if (isLocalPlayer)
            {
                if (executor.World.Has<PlayerSceneCRDTEntity>(sceneWorldEntity))
                    executor.World.Remove<PlayerSceneCRDTEntity>(sceneWorldEntity);
            }
            else
                executor.World.Add<DeleteEntityIntention>(sceneWorldEntity);
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
