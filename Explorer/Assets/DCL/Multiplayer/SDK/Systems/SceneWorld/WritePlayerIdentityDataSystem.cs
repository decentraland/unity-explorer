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
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WritePlayerIdentityDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WritePlayerIdentityDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        public override void Initialize()
        {
            CreatePlayerIdentityDataQuery(World, true);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            CreatePlayerIdentityDataQuery(World, false);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CreatePlayerIdentityData([Data] bool force, PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile)
        {
            if (!force && !playerCRDTEntity.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, (string address, bool isGuest)>(static (pbComponent, data) =>
            {
                pbComponent.Address = data.address;
                pbComponent.IsGuest = data.isGuest;
            }, playerCRDTEntity.CRDTEntity, (profile.UserId!, !profile.HasConnectedWeb3));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerIdentityData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
