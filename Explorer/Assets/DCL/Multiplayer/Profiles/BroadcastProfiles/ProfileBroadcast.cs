using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Kernel.Comms.Rfc4;
using System.Threading;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class ProfileBroadcast : IProfileBroadcast
    {
        private const int CURRENT_PROFILE_VERSION = 0;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly ISelfProfile selfProfile;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public ProfileBroadcast(IMessagePipesHub messagePipesHub,
            ISelfProfile selfProfile)
        {
            this.messagePipesHub = messagePipesHub;
            this.selfProfile = selfProfile;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void NotifyRemotes()
        {
            SendTo(messagePipesHub.IslandPipe());
            SendTo(messagePipesHub.ScenePipe());
        }

        private void SendTo(IMessagePipe messagePipe)
        {
            async UniTaskVoid GetProfileVersionThenSendAsync(CancellationToken ct)
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);
                MessageWrap<AnnounceProfileVersion> message = messagePipe.NewMessage<AnnounceProfileVersion>();
                message.Payload.ProfileVersion = (uint)(profile?.Version ?? CURRENT_PROFILE_VERSION);
                message.SendAndDisposeAsync(ct).Forget();
            }

            GetProfileVersionThenSendAsync(cancellationTokenSource.Token).Forget();
        }
    }
}
