using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PLAYER_IDENTITY_DATA)]
    public partial class WritePlayerIdentityDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WritePlayerIdentityDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            RemovePlayerIdentityDataQuery(World);
            CreatePlayerIdentityDataQuery(World);
        }

        // 'PlayerIdentityDataComponent' is put on this scene world entities from the GLOBAL WORLD PlayerComponentsHandlerSystem
        [Query]
        [None(typeof(PBPlayerIdentityData))]
        private void CreatePlayerIdentityData(in Entity entity, PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            Debug.Log($"PRAVS - CreatePlayerIdentityDataQuery() - Entity: {entity.Id}; CRDTEntity: {playerIdentityDataComponent.CRDTEntity.Id}; Address: {playerIdentityDataComponent.Address}");

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, PlayerIdentityDataComponent>(static (pbPlayerIdentityData, playerIdentityDataComponent) =>
            {
                // pbPlayerIdentityData.IsDirty = true;
                pbPlayerIdentityData.Address = playerIdentityDataComponent.Address;
                pbPlayerIdentityData.IsGuest = playerIdentityDataComponent.IsGuest;
            }, playerIdentityDataComponent.CRDTEntity, playerIdentityDataComponent);

            World.Add(entity, new PBPlayerIdentityData
            {
                Address = playerIdentityDataComponent.Address,
                IsGuest = playerIdentityDataComponent.IsGuest,
            }, playerIdentityDataComponent.CRDTEntity);
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
