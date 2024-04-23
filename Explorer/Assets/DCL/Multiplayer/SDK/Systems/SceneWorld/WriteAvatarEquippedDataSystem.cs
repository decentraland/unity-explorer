using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [UpdateAfter(typeof(PlayerComponentsHandlerSystem))]
    [LogCategory(ReportCategory.PLAYER_AVATAR_EQUIPPED)]
    public partial class WriteAvatarEquippedDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBAvatarEquippedData> componentPool;

        public WriteAvatarEquippedDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBAvatarEquippedData> componentPool) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.componentPool = componentPool;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEquippedDataQuery(World);
            CreateAvatarEquippedDataQuery(World);
        }

        [Query]
        [None(typeof(PBAvatarEquippedData), typeof(DeleteEntityIntention))]
        private void CreateAvatarEquippedData(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            PBAvatarEquippedData pbComponent = componentPool.Get();
            pbComponent.WearableUrns.Clear();

            foreach (URN urn in playerProfileDataComponent.WearableUrns) { pbComponent.WearableUrns.Add(urn); }

            pbComponent.EmoteUrns.Clear();

            foreach (URN urn in playerProfileDataComponent.EmoteUrns) { pbComponent.EmoteUrns.Add(urn); }

            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, PBAvatarEquippedData>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.WearableUrns.Clear();

                foreach (URN urn in pbComponent.WearableUrns) { dispatchedPBComponent.WearableUrns.Add(urn); }

                dispatchedPBComponent.EmoteUrns.Clear();

                foreach (URN urn in pbComponent.EmoteUrns) { dispatchedPBComponent.EmoteUrns.Add(urn); }
            }, playerProfileDataComponent.CRDTEntity, pbComponent);

            World.Add(entity, pbComponent, playerProfileDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEquippedData(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent, ref PBAvatarEquippedData pbComponent)
        {
            if (!playerProfileDataComponent.IsDirty) return;

            pbComponent.WearableUrns.Clear();

            foreach (URN urn in playerProfileDataComponent.WearableUrns) { pbComponent.WearableUrns.Add(urn); }

            pbComponent.EmoteUrns.Clear();

            foreach (URN urn in playerProfileDataComponent.EmoteUrns) { pbComponent.EmoteUrns.Add(urn); }

            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, PBAvatarEquippedData>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.WearableUrns.Clear();

                foreach (URN urn in pbComponent.WearableUrns) { dispatchedPBComponent.WearableUrns.Add(urn); }

                dispatchedPBComponent.EmoteUrns.Clear();

                foreach (URN urn in pbComponent.EmoteUrns) { dispatchedPBComponent.EmoteUrns.Add(urn); }
            }, playerProfileDataComponent.CRDTEntity, pbComponent);

            World.Set(entity, pbComponent);
        }

        [Query]
        [All(typeof(PBAvatarEquippedData))]
        [None(typeof(PlayerProfileDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEquippedData>(crdtEntity);
            World.Remove<PBAvatarEquippedData, CRDTEntity>(entity);
        }
    }
}
