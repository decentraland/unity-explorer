using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using System.Threading;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class ProfileBroadcast : IProfileBroadcast
    {
        private const int CURRENT_PROFILE_VERSION = 0;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public ProfileBroadcast(IMessagePipesHub messagePipesHub)
        {
            this.messagePipesHub = messagePipesHub;
        }

        public UniTaskVoid NotifyRemotesAsync()
        {
            SendTo(messagePipesHub.IslandPipe());
            SendTo(messagePipesHub.ScenePipe());
            return new UniTaskVoid();
        }

        private void SendTo(IMessagePipe messagePipe)
        {
            var message = messagePipe.NewMessage<AnnounceProfileVersion>();
            message.Payload.ProfileVersion = CURRENT_PROFILE_VERSION;
            message.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
