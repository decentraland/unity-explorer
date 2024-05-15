using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
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
            HandleComponentRemovalQuery(World);
            CreatePlayerIdentityDataQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CreatePlayerIdentityData(PlayerCRDTEntity playerCRDTEntity, Profile profile)
        {
            if (!playerCRDTEntity.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, (string address, bool isGuest)>(static (pbComponent, data) =>
            {
                pbComponent.Address = data.address;
                pbComponent.IsGuest = data.isGuest;
            }, playerCRDTEntity.CRDTEntity, (profile.UserId, !profile.HasConnectedWeb3));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
