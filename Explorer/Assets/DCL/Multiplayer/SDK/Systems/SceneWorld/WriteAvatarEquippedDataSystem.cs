using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateAfter(typeof(WritePlayerIdentityDataSystem))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<ProfileSDKSubProduct>))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WriteAvatarEquippedDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteAvatarEquippedDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        public override void Initialize()
        {
            UpdateAvatarEquippedDataQuery(World, true);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEquippedDataQuery(World, false);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEquippedData([Data] bool force, PlayerSceneCRDTEntity crdtEntity, ProfileSDKSubProduct profile)
        {
            if (!force && !profile.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, ProfileSDKSubProduct>(static (pbComponent, profile) =>
            {
                foreach (URN urn in profile.Avatar.Wearables) { pbComponent.WearableUrns.Add(urn); }

                foreach (URN urn in profile.Avatar.Emotes)
                {
                    if (!urn.IsNullOrEmpty())
                        pbComponent.EmoteUrns.Add(urn);
                }
            }, crdtEntity.CRDTEntity, profile);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEquippedData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
