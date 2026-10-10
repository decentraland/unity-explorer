using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Pulse;
using Pulse.Transport;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>Main thread only: the last announcement is kept without synchronization.</summary>
    public class PulseProfilePropagationBus : IProfilePropagation
    {
        private readonly IPulseMultiplayerService service;

        private UserId? lastAnnouncedUserId;
        private int lastAnnouncedVersion;

        public PulseProfilePropagationBus(IPulseMultiplayerService service)
        {
            this.service = service;
        }

        /// <summary>Announces each user's profile version once; a copy at an announced version is not sent again.</summary>
        public void PropagateIfNewVersion(Profile profile)
        {
            if (!service.IsAuthenticated || (profile.Version == lastAnnouncedVersion && profile.UserId.Equals(lastAnnouncedUserId)))
                return;

            lastAnnouncedUserId = profile.UserId;
            lastAnnouncedVersion = profile.Version;

            var message = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.ProfileAnnouncement);

            message.Message.ProfileAnnouncement = new ProfileVersionAnnouncement
            {
                Version = profile.Version,
            };

            service.Send(message);
        }
    }
}
