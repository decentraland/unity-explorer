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
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_COMPONENTS_HANDLER)]
    public partial class PlayerComponentsHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;

        // TODO: Clear somehow disposed scenes...
        private readonly IDictionary<Vector2Int, SceneReservedCRDTEntitiesData> scenesReservedEntities = new Dictionary<Vector2Int, SceneReservedCRDTEntitiesData>();

        public PlayerComponentsHandlerSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UpdatePlayerIdentityDataQuery(World);
        }

        [Query]
        [None(typeof(PlayerIdentityDataComponent))]
        private void UpdatePlayerIdentityData(in Entity entity, ref Profile profile, ref CharacterTransform characterTransform)
        {
            if (!scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
                return;

            if (!scenesReservedEntities.TryGetValue(sceneFacade.Info.BaseParcel, out SceneReservedCRDTEntitiesData sceneReservedCRDTEntitiesData))
            {
                sceneReservedCRDTEntitiesData = new SceneReservedCRDTEntitiesData(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
                scenesReservedEntities.Add(sceneFacade.Info.BaseParcel, sceneReservedCRDTEntitiesData);
            }

            int crdtEntityId = sceneReservedCRDTEntitiesData.ReserveNextFreeEntity();

            // All reserved entities are taken
            if (crdtEntityId == -1) return;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneFacade.EcsExecutor.Sync.GetScope())
            {
                var crdtEntityComponent = new CRDTEntity(crdtEntityId);
                Debug.Log($"PRAVS - UpdatePlayerIdentityDataQuery() - Entity: {entity.Id}; CRDTEntity: {crdtEntityComponent.Id}; Address: {profile.UserId}; scene parcel: {sceneFacade.Info.BaseParcel}");

                World sceneWorld = sceneFacade.EcsExecutor.World;

                var playerIdentityData = new PlayerIdentityDataComponent
                {
                    CRDTEntity = crdtEntityComponent,
                    Address = profile.UserId,
                    IsGuest = !profile.HasConnectedWeb3, // TODO: check if this assumption is correct
                };

                World.Add(entity, playerIdentityData);

                // sceneWorld.Add(entity, playerIdentityData, crdtEntityComponent);
                sceneWorld.Add(entity, playerIdentityData);
            }
        }

        private struct SceneReservedCRDTEntitiesData
        {
            private readonly bool[] reservedEntities;

            public SceneReservedCRDTEntitiesData(int entitiesAmount)
            {
                reservedEntities = new bool[entitiesAmount];
            }

            // TODO: Optimize
            public int ReserveNextFreeEntity()
            {
                for (var i = 0; i < reservedEntities.Length; i++)
                {
                    if (!reservedEntities[i])
                    {
                        reservedEntities[i] = true;
                        return SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + i;
                    }
                }

                // All reserved entities are taken
                return -1;
            }

            public void ClearReservedEntity(int entityId)
            {
                entityId = entityId - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;
                if (entityId >= reservedEntities.Length) return;

                reservedEntities[entityId] = false;
            }
        }
    }
}
