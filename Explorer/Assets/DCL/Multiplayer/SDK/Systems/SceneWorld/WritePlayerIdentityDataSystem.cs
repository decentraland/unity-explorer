using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    [LogCategory(ReportCategory.PLAYER_IDENTITY_DATA)]
    public partial class WritePlayerIdentityDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBPlayerIdentityData> componentPool;

        public WritePlayerIdentityDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBPlayerIdentityData> componentPool) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.componentPool = componentPool;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            CreatePlayerIdentityDataQuery(World);
        }

        [Query]
        [None(typeof(PBPlayerIdentityData), typeof(DeleteEntityIntention))]
        private void CreatePlayerIdentityData(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            PBPlayerIdentityData? pbComponent = componentPool.Get();
            pbComponent.Address = playerProfileDataComponent.Address;
            pbComponent.IsGuest = playerProfileDataComponent.IsGuest;

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, PBPlayerIdentityData>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Address = pbComponent.Address;
                dispatchedPBComponent.IsGuest = pbComponent.IsGuest;
            }, playerProfileDataComponent.CRDTEntity, pbComponent);

            World.Add(entity, pbComponent, playerProfileDataComponent.CRDTEntity);
        }

        [Query]
        [All(typeof(PBPlayerIdentityData))]
        [None(typeof(PlayerProfileDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBPlayerIdentityData, CRDTEntity>(entity);
        }
    }
}
