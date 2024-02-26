using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using ECS.Abstract;

namespace DCL.Multiplayer.Profiles.Systems
{
    /// <summary>
    ///     The scheme
    ///     1 receive signal announce profile
    ///     2 fetch the profile
    ///     3 assign profile to the entity
    ///     4 auto flow of avatar
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MultiplayerProfilesSystem : BaseUnityLoopSystem
    {
        private readonly IRemoteAnnouncements remoteAnnouncements;
        private readonly IRemoteProfiles remoteProfiles;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IRemoteEntities remoteEntities;

        public MultiplayerProfilesSystem(
            World world,
            IRemoteAnnouncements remoteAnnouncements,
            IRemoteProfiles remoteProfiles,
            IProfileBroadcast profileBroadcast,
            IRemoteEntities remoteEntities
        ) : base(world)
        {
            this.remoteAnnouncements = remoteAnnouncements;
            this.remoteProfiles = remoteProfiles;
            this.profileBroadcast = profileBroadcast;
            this.remoteEntities = remoteEntities;
        }

        protected override void Update(float t)
        {
            remoteProfiles.Download(remoteAnnouncements);
            remoteEntities.TryCreate(remoteProfiles, World!);
            profileBroadcast.NotifyRemotesAsync();
        }
    }
}
