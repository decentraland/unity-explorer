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

        /// <summary>
        ///     The version (generation) each reserved entity number is currently handed out with, `-1` while
        ///     it was never handed out at all. ADR-245 requires a new generation every time a number is
        ///     recycled: a scene keeps its deleted entities as `number -> version` and drops every message
        ///     whose version is not greater than the stored one, so a number re-issued with the same version
        ///     stays invisible to that scene until it is reloaded.
        /// </summary>
        private readonly int[] reservedEntityVersions = new int[SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM];

        private int currentReservedEntitiesCount;

        /// <summary>
        ///     Guards the exhaustion warning so it is reported once per exhaustion episode
        ///     instead of once per entity per frame
        /// </summary>
        private bool reservedEntitiesExhaustionReported;

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
            CRDTEntity crdtEntity;

            if (World.Has<PlayerComponent>(entity))
                crdtEntity = SpecialEntitiesID.PLAYER_ENTITY;
            else if (!TryReserveNextFreeEntity(out crdtEntity))
            {
                // All reserved entities are taken: this player can't be exposed to any scene at all
                if (!reservedEntitiesExhaustionReported)
                {
                    reservedEntitiesExhaustionReported = true;

                    ReportHub.LogWarning(GetReportData(),
                        $"All {reservedEntities.Length} reserved CRDT entities are taken: remote players can't be exposed to scenes anymore. "
                        + "Newly connected players will stay invisible to every scene until a slot is released.");
                }

                return;
            }

            var playerCRDTEntity = new PlayerCRDTEntity(crdtEntity);

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
                    && globalPlayerCRDTEntity.SceneFacade is not null) { RemovePlayerFromScene(globalPlayerCRDTEntity.SceneWorldEntity, reservedEntityId, globalPlayerCRDTEntity.SceneFacade); }

                if (newSceneIsValid)
                {
                    SceneEcsExecutor sceneEcsExecutor = currentScene.EcsExecutor;

                    bool isLocalPlayer = reservedEntityId.Id == SpecialEntitiesID.PLAYER_ENTITY;

                    // LocalPlayerCRDTEntityHandlerSystem creates PlayerSceneCRDTEntity on scene start-up
                    Entity sceneWorldEntity = isLocalPlayer
                        ? currentScene.PersistentEntities.Player
                        : sceneEcsExecutor.World.Create(new PlayerSceneCRDTEntity(reservedEntityId));

                    globalPlayerCRDTEntity.AssignToScene(currentScene, sceneWorldEntity);
                }
                else
                    globalPlayerCRDTEntity.RemoveFromScene();
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
                    RemovePlayerFromScene(playerCRDTEntity.SceneWorldEntity, playerCRDTEntity.CRDTEntity, playerCRDTEntity.SceneFacade);
            }

            if (noLongerExists)
            {
                // The reservation is bound to the component, not to the scene assignment: it is taken unconditionally
                // in `AddPlayerCRDTEntity` so it must be released whenever the component goes away, otherwise players
                // that disconnect while being in no scene (hidden spawn position, roads, empty parcels, LOD, realm change)
                // leak their slot forever
                FreeReservedEntity(playerCRDTEntity.CRDTEntity);

                World.Remove<PlayerCRDTEntity>(entity);
            }
        }

        private static void RemovePlayerFromScene(Entity sceneWorldEntity, CRDTEntity crdtEntity, ISceneFacade sceneFacade)
        {
            // Local Player is never removed from the scene world
            if (crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY)
                return;

            SceneState state = sceneFacade.SceneStateProvider.State.Value();

            if (state != SceneState.Running && state != SceneState.Starting)
                return;

            sceneFacade.EcsExecutor.World.Add<DeleteEntityIntention>(sceneWorldEntity);
        }

        private bool TryReserveNextFreeEntity(out CRDTEntity crdtEntity)
        {
            crdtEntity = default;

            // All reserved entities are taken
            if (currentReservedEntitiesCount == reservedEntities.Length)
                return false;

            for (var i = 0; i < reservedEntities.Length; i++)
            {
                if (reservedEntities[i]) continue;

                reservedEntities[i] = true;
                currentReservedEntitiesCount++;

                // A number that was never handed out starts at version 0, every recycle advances it
                int version = ++reservedEntityVersions[i];

                crdtEntity = CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + i, version);
                return true;
            }

            return false;
        }

        private void FreeReservedEntity(CRDTEntity crdtEntity)
        {
            // Ids outside the reserved range (e.g. the local player's PLAYER_ENTITY) are not pooled
            int index = crdtEntity.EntityNumber - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;
            if (index >= reservedEntities.Length || index < 0) return;

            // Idempotent on purpose: releasing an already free slot must not corrupt the count
            if (!reservedEntities[index]) return;

            // The number ran out of versions: handing it out again would repeat a generation that
            // scenes may still hold as deleted, so the slot stays taken and is retired for good
            if (reservedEntityVersions[index] >= CRDTEntity.MAX_VERSION)
            {
                ReportHub.LogWarning(GetReportData(),
                    $"Reserved CRDT entity number {crdtEntity.EntityNumber} ran out of versions and is retired: "
                    + "the pool of ids exposed to scenes shrinks by one for the rest of the session.");

                return;
            }

            reservedEntities[index] = false;
            currentReservedEntitiesCount--;
            reservedEntitiesExhaustionReported = false;
        }

        private void ClearReservedEntities()
        {
            for (var i = 0; i < reservedEntities.Length; i++)
            {
                reservedEntities[i] = false;

                // `-1` so the first reservation of every number is handed out with version 0
                reservedEntityVersions[i] = -1;
            }

            currentReservedEntitiesCount = 0;
            reservedEntitiesExhaustionReported = false;
        }
    }
}
