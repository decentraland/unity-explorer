using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using Decentraland.Kernel.Comms.Rfc4;
using DCL.LiveKit.Public;
using System.Threading;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class LiveKitProfileBroadcast : IProfileBroadcast
    {
        private const int CURRENT_PROFILE_VERSION = 0;
        private readonly ISelfProfile selfProfile;
        private readonly LiveKitMessagesBroadcaster broadcaster;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public LiveKitProfileBroadcast(ISelfProfile selfProfile,
            LiveKitMessagesBroadcaster broadcaster)
        {
            this.selfProfile = selfProfile;
            this.broadcaster = broadcaster;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void NotifyRemotes()
        {
            async UniTaskVoid GetProfileVersionThenSendAsync(CancellationToken ct)
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);

                broadcaster.Send<Profile?, AnnounceProfileVersion>(static (p, version) => BuildMessage(p, version), profile, LKDataPacketKind.KindReliable, ct);
            }

            GetProfileVersionThenSendAsync(cancellationTokenSource.Token).Forget();
        }

        private static void BuildMessage(Profile? profile, AnnounceProfileVersion payload)
        {
            payload.ProfileVersion = (uint)(profile?.Version ?? CURRENT_PROFILE_VERSION);
        }
    }
}
