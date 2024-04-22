using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
        private void CreatePlayerIdentityData(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            PBPlayerIdentityData? pbComponent = componentPool.Get();
            pbComponent.Address = playerSDKDataComponent.Address;
            pbComponent.IsGuest = playerSDKDataComponent.IsGuest;

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, PBPlayerIdentityData>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Address = pbComponent.Address;
                dispatchedPBComponent.IsGuest = pbComponent.IsGuest;
            }, playerSDKDataComponent.CRDTEntity, pbComponent);

            World.Add(entity, pbComponent, playerSDKDataComponent.CRDTEntity);
        }

        [Query]
        [All(typeof(PBPlayerIdentityData))]
        [None(typeof(PlayerSDKDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBPlayerIdentityData, CRDTEntity>(entity);
        }
    }
}
