using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Pulse;
using Pulse.Transport;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseProfilePropagationBus : IProfilePropagation
    {
        private readonly IPulseMultiplayerService service;

        private Profile? lastAnnounced;
        private int lastAnnouncedVersion;

        public PulseProfilePropagationBus(IPulseMultiplayerService service)
        {
            this.service = service;
        }

        /// <summary>Announces the profile version once; the same instance at the same version is not sent again.</summary>
        public void PropagateIfNewVersion(Profile profile)
        {
            if (ReferenceEquals(profile, lastAnnounced) && profile.Version == lastAnnouncedVersion)
                return;

            lastAnnounced = profile;
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
