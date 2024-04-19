using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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

        // 'PlayerIdentityDataComponent' is put on this scene world entities from the GLOBAL WORLD PlayerComponentsHandlerSystem
        [Query]
        [None(typeof(PBPlayerIdentityData), typeof(DeleteEntityIntention))]
        private void CreatePlayerIdentityData(in Entity entity, PlayerIdentityDataComponent playerIdentityDataComponent)
        {
            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, PlayerIdentityDataComponent>(static (pbPlayerIdentityData, playerIdentityDataComponent) =>
            {
                pbPlayerIdentityData.Address = playerIdentityDataComponent.Address;
                pbPlayerIdentityData.IsGuest = playerIdentityDataComponent.IsGuest;
            }, playerIdentityDataComponent.CRDTEntity, playerIdentityDataComponent);

            PBPlayerIdentityData? pbComponent = componentPool.Get();
            pbComponent.Address = playerIdentityDataComponent.Address;
            pbComponent.IsGuest = playerIdentityDataComponent.IsGuest;
            World.Add(entity, pbComponent, playerIdentityDataComponent.CRDTEntity);
        }

        [Query]
        [All(typeof(PBPlayerIdentityData))]
        [None(typeof(PlayerIdentityDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBPlayerIdentityData, CRDTEntity>(entity);
        }
    }
}
