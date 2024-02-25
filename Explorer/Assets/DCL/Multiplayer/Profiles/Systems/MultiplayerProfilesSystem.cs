using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Systems
{
    /// <summary>
    /// The scheme
    /// 1 receive signal announce profile
    /// 2 fetch the profile
    /// 3 assign profile to the entity
    /// 4 auto flow of avatar
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MultiplayerProfilesSystem : BaseUnityLoopSystem
    {
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IRemoteAnnouncements remoteAnnouncements;
        private readonly IRemoteProfiles remoteProfiles;
        private readonly IProfileBroadcast profileBroadcast;

        public MultiplayerProfilesSystem(
            World world,
            IEntityParticipantTable entityParticipantTable,
            IRemoteAnnouncements remoteAnnouncements,
            IRemoteProfiles remoteProfiles,
            IProfileBroadcast profileBroadcast
        ) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.remoteAnnouncements = remoteAnnouncements;
            this.remoteProfiles = remoteProfiles;
            this.profileBroadcast = profileBroadcast;
        }

        protected override void Update(float t)
        {
            if (remoteAnnouncements.NewBunchAvailable())
            {
                using var bunch = remoteAnnouncements.Bunch();
                var list = bunch.Collection();
                remoteProfiles.Download(list);
            }

            if (remoteProfiles.NewBunchAvailable())
            {
                using var bunch = remoteProfiles.Bunch();
                var collection = bunch.Collection();
                TryCreateRemoteEntities(collection);
            }

            profileBroadcast.NotifyRemotesAsync();
        }

        private void TryCreateRemoteEntities(IEnumerable<RemoteProfile> list)
        {
            foreach (RemoteProfile remoteProfile in list)
                TryCreateRemoteEntity(remoteProfile);
        }

        private void TryCreateRemoteEntity(in RemoteProfile profile)
        {
            if (entityParticipantTable.Has(profile.WalletId))
                return;

            var entity = World!.Create(profile.Profile);
            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
