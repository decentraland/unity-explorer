using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PLAYER_IDENTITY_DATA)]
    public partial class WritePlayerIdentityDataSystem : BaseUnityLoopSystem
    {
        // private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WritePlayerIdentityDataSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            // this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            // TODO: Check if not current scene then release currently tracked player entities or similar???

            RemovePlayerIdentityDataQuery(World);
            CreatePlayerIdentityDataQuery(World);
        }

        // for testing only
        /*internal static int lastReservedEntityId = 31;*/

        // Put 'PlayerIdentityDataComponent' on this scene world entities from a GLOBAL WORLD system...
        [Query]
        [None(typeof(PBPlayerIdentityData))]
        private void CreatePlayerIdentityData(in Entity entity, ref CRDTEntity crdtEntity, PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            /*var playerIdentityDataComponent = new PlayerIdentityDataComponent()
            {
                entityId = ++lastReservedEntityId
            };
            World.Add(entity, playerIdentityDataComponent);*/

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, PlayerIdentityDataComponent>(static (pbPlayerIdentityData, playerIdentityDataComponent) =>
            {
                // pbPlayerIdentityData.IsDirty = true;
                pbPlayerIdentityData.Address = playerIdentityDataComponent.Address;
                pbPlayerIdentityData.IsGuest = playerIdentityDataComponent.IsGuest;
            }, crdtEntity, playerIdentityDataComponent);
        }

        [Query]
        [All(typeof(PBPlayerIdentityData))]
        [None(typeof(PlayerIdentityDataComponent))]
        private void RemovePlayerIdentityData(in Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(crdtEntity);

            World.Add(entity, new DeleteEntityIntention());
        }
    }
}
