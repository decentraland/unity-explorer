using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Pulse;
using Pulse.Transport;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseProfilePropagationBus : IProfilePropagation
    {
        private readonly IPulseMultiplayerService service;

        public PulseProfilePropagationBus(IPulseMultiplayerService service)
        {
            this.service = service;
        }

        public void Propagate(Profile profile)
        {
            var message = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.ProfileAnnouncement);

            message.Message.ProfileAnnouncement = new ProfileVersionAnnouncement
            {
                Version = profile.Version,
            };

            service.Send(message);
        }
    }
}
