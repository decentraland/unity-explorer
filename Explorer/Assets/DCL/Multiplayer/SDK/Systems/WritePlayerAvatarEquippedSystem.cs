using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PLAYER_AVATAR_EQUIPPED)]
    public partial class WritePlayerAvatarEquippedSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBAvatarEquippedData> componentPool;

        public WritePlayerAvatarEquippedSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBAvatarEquippedData> componentPool) : base(world)
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
        private void CreateAvatarEquippedData(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, PlayerSDKDataComponent>(static (pbAvatarEquippedData, playerSDKDataComponent) =>
            {
                pbAvatarEquippedData.WearableUrns.Clear();

                foreach (URN urn in playerSDKDataComponent.WearableUrns) { pbAvatarEquippedData.WearableUrns.Add(urn); }

                pbAvatarEquippedData.EmoteUrns.Clear();

                foreach (URN urn in playerSDKDataComponent.EmoteUrns) { pbAvatarEquippedData.EmoteUrns.Add(urn); }
            }, playerSDKDataComponent.CRDTEntity, playerSDKDataComponent);

            PBAvatarEquippedData pbComponent = componentPool.Get();
            pbComponent.WearableUrns.Clear();

            foreach (URN urn in playerSDKDataComponent.WearableUrns) { pbComponent.WearableUrns.Add(urn); }

            pbComponent.EmoteUrns.Clear();

            foreach (URN urn in playerSDKDataComponent.EmoteUrns) { pbComponent.EmoteUrns.Add(urn); }

            World.Add(entity, pbComponent, playerSDKDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEquippedData(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent, ref PBAvatarEquippedData pbComponent)
        {
            if (!playerSDKDataComponent.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, PlayerSDKDataComponent>(static (pbAvatarEquippedData, playerSDKDataComponent) =>
            {
                pbAvatarEquippedData.WearableUrns.Clear();

                foreach (URN urn in playerSDKDataComponent.WearableUrns) { pbAvatarEquippedData.WearableUrns.Add(urn); }

                pbAvatarEquippedData.EmoteUrns.Clear();

                foreach (URN urn in playerSDKDataComponent.EmoteUrns) { pbAvatarEquippedData.EmoteUrns.Add(urn); }
            }, playerSDKDataComponent.CRDTEntity, playerSDKDataComponent);

            pbComponent.WearableUrns.Clear();

            foreach (URN urn in playerSDKDataComponent.WearableUrns) { pbComponent.WearableUrns.Add(urn); }

            pbComponent.EmoteUrns.Clear();

            foreach (URN urn in playerSDKDataComponent.EmoteUrns) { pbComponent.EmoteUrns.Add(urn); }

            World.Set(entity, pbComponent);
        }

        [Query]
        [All(typeof(PBAvatarEquippedData))]
        [None(typeof(PlayerSDKDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEquippedData>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBAvatarEquippedData, CRDTEntity>(entity);
        }
    }
}
