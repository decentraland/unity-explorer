using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Unity.Groups;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PLAYER_IDENTITY_DATA)]
    public partial class PlayerIdentityDataSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription USER_PROFILES_QUERY = new QueryDescription().WithAll<Profile>();
        private readonly World globalWorld;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        // private CreatePlayerIdentityData createPlayerIdentityData;

        public PlayerIdentityDataSystem(World world, ObjectProxy<World> globalWorldProxy, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            globalWorld = globalWorldProxy.Object!;
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;

            // createPlayerIdentityData = new CreatePlayerIdentityData(World, ecsToCRDTWriter);
        }

        protected override void Update(float t)
        {
            // TODO: Check if not current scene then release currently tracked player entities???

            // Create Player Identity Data...

            // IForEach<Entity, Profile> doesn't work...
            // globalWorld.InlineQuery<CreatePlayerIdentityData, Profile>(in USER_PROFILES_QUERY, ref createPlayerIdentityData);

            // possible alternative...
            globalWorld.Query(USER_PROFILES_QUERY,
                    (Entity entity, ref Profile profileComponent) =>
                    {
                        if (World.Has<PlayerIdentityDataComponent>(entity)) return;

                        var playerIdentityDataComponent = new PlayerIdentityDataComponent()
                        {
                            entityId = ++lastReservedEntityId
                        };
                        World.Add(entity, playerIdentityDataComponent);

                        ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, Profile>(static (pbPlayerIdentityData, profile) =>
                        {
                            // pbPlayerIdentityData.IsDirty = true;
                            pbPlayerIdentityData.Address = profile.UserId;
                            pbPlayerIdentityData.IsGuest = !profile.HasConnectedWeb3;
                        }, playerIdentityDataComponent.entityId, profileComponent);
                    });
        }

        // for testing only
        internal static int lastReservedEntityId = 31;
        /*private readonly struct CreatePlayerIdentityData : IForEach<Profile>
        {
            private readonly World world;
            private readonly IECSToCRDTWriter ecsToCRDTWriter;

            public CreatePlayerIdentityData(World world, IECSToCRDTWriter ecsToCRDTWriter)
            {
                this.world = world;
                this.ecsToCRDTWriter = ecsToCRDTWriter;
            }

            public void Update(ref Profile profileComponent)
            {
                Debug.Log("PRAVS - CreatePlayerIdentityData.Update()! - 1");

                if (world.Has<PlayerIdentityDataComponent>(entity)) return;

                Debug.Log("PRAVS - CreatePlayerIdentityData.Update()! - 2");

                // var pbPlayerIdentityData = new PBPlayerIdentityData()
                // {
                //     Address = profileComponent.UserId,
                //     IsGuest = !profileComponent.HasConnectedWeb3
                // };

                var playerIdentityDataComponent = new PlayerIdentityDataComponent()
                {
                    entityId = lastReservedEntityId++
                };

                // world.Add(entity, playerIdentityDataComponent, pbPlayerIdentityData);
                world.Add(entity, playerIdentityDataComponent);

                ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, Profile>(static (pbPlayerIdentityData, profile) =>
                {
                    // pbPlayerIdentityData.IsDirty = true;
                    pbPlayerIdentityData.Address = profile.UserId;
                    pbPlayerIdentityData.IsGuest = !profile.HasConnectedWeb3;
                }, playerIdentityDataComponent.entityId, profileComponent);
            }
        }*/
    }
}
