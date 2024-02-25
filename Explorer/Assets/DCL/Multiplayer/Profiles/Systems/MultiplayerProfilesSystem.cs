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
                var list = bunch.List();
                CreateRemoteEntities(list);
            }

            profileBroadcast.NotifyRemotesAsync();

            //TODO remove scheme
            //1 receive signal announce profile
            //2 fetch the profile
            //3 assign profile to the entity
            //4 auto flow of avatar
        }

        private void CreateRemoteEntities(IEnumerable<RemoteProfile> list)
        {
            foreach (RemoteProfile remoteProfile in list)
            {
                var entity = World!.Create(remoteProfile.Profile);
                entityParticipantTable.Register(remoteProfile.WalletId, entity);
            }
        }
    }
}
