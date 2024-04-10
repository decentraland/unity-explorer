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
            if (scenesCache.TryGetByParcel(ParcelMathHelper.FloorToParcel(characterTransform.Transform.position), out ISceneFacade sceneFacade))
            {
                var crdtEntityComponent = new CRDTEntity();

                Debug.Log($"PRAVS - UpdatePlayerIdentityDataQuery() - CRDTEntity: {crdtEntityComponent}; Address: {profile.UserId}; scene parcel: {sceneFacade.Info.BaseParcel}");

                World.Add(entity, new PlayerIdentityDataComponent()
                {
                    CRDTEntity = crdtEntityComponent,
                    Address = profile.UserId,
                    IsGuest = !profile.HasConnectedWeb3 // TODO: check if this assumption is correct
                }, crdtEntityComponent);
            }
        }
    }
}
