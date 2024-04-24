using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<Profile>))]
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
        private void CreatePlayerIdentityData(in Entity entity, PlayerCRDTEntity playerCRDTEntity, Profile profile)
        {
            PBPlayerIdentityData? pbComponent = componentPool.Get();
            pbComponent.Address = profile.UserId;
            pbComponent.IsGuest = !profile.HasConnectedWeb3;

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, (string address, bool isGuest)>(static (pbComponent, data) =>
            {
                pbComponent.Address = data.address;
                pbComponent.IsGuest = data.isGuest;
            }, playerCRDTEntity.CRDTEntity, (profile.UserId, !profile.HasConnectedWeb3));

            World.Add(entity, pbComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref PlayerCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
