using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
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
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_COMPONENTS_HANDLER)]
    public partial class PlayerComponentsHandlerSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;

        // FOR TESTING ONLY
        private int crdtEntityId = 32;

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

            // External world access should be always synchronized (Global World calls into Scene World)
            // using (sceneFacade.EcsExecutor.Sync.GetScope())
            {
                var crdtEntityComponent = new CRDTEntity(crdtEntityId++);
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
    }
}
