using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using Decentraland.Kernel.Comms.Rfc4;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class ProfileBroadcast : IProfileBroadcast
    {
        private const int CURRENT_PROFILE_VERSION = 0;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IRoomHub roomHub;

        public ProfileBroadcast(IMessagePipesHub messagePipesHub, IRoomHub roomHub)
        {
            this.messagePipesHub = messagePipesHub;
            this.roomHub = roomHub;
        }

        public UniTaskVoid NotifyRemotesAsync()
        {
            var message = messagePipesHub.IslandPipe().NewMessage<AnnounceProfileVersion>();
            message.Payload.ProfileVersion = CURRENT_PROFILE_VERSION;
            message.AddRecipients(roomHub.IslandRoom());
            return message.SendAndDisposeAsync();
        }
    }
}
