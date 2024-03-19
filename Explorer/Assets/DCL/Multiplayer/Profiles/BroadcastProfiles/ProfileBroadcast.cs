using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Rooms;

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
            SendTo(messagePipesHub.IslandPipe(), roomHub.IslandRoom());
            SendTo(messagePipesHub.ScenePipe(), roomHub.SceneRoom());
            return new UniTaskVoid();
        }

        private static void SendTo(IMessagePipe messagePipe, IRoom room)
        {
            var message = messagePipe.NewMessage<AnnounceProfileVersion>();
            message.Payload.ProfileVersion = CURRENT_PROFILE_VERSION;
            message.AddRecipients(room);
            message.SendAndDisposeAsync().Forget();
        }
    }
}
