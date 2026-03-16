using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Pulse;

namespace DCL.Multiplayer.Movement.Systems
{
    public class PulseProfilePropagationBus : IProfilePropagation
    {
        private readonly PulseMultiplayerService service;

        public PulseProfilePropagationBus(PulseMultiplayerService service)
        {
            this.service = service;
        }

        public void Propagate(Profile profile)
        {
            var message = MessagePipe.OutgoingMessage.Create(ITransport.PacketMode.RELIABLE, ClientMessage.MessageOneofCase.ProfileAnnouncement);

            message.Message.ProfileAnnouncement = new ProfileVersionAnnouncement
            {
                Version = profile.Version,
            };

            service.Send(message);
        }
    }
}
