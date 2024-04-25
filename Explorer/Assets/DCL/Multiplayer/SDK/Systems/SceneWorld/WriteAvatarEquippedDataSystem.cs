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
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateAfter(typeof(WritePlayerIdentityDataSystem))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<Profile>))]
    [LogCategory(ReportCategory.PLAYER_AVATAR_EQUIPPED)]
    public partial class WriteAvatarEquippedDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteAvatarEquippedDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEquippedDataQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEquippedData(ref PlayerCRDTEntity playerCRDTEntity, ref Profile profile)
        {
            if (!profile.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, Profile>(static (pbComponent, profile) =>
            {
                foreach (URN urn in profile.Avatar.Wearables) { pbComponent.WearableUrns.Add(urn); }

                foreach (URN urn in profile.Avatar.Emotes) { pbComponent.EmoteUrns.Add(urn); }
            }, playerCRDTEntity.CRDTEntity, profile);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref PlayerCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEquippedData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
