using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class EnsureSelfPublishedProfileBroadcast : IProfileBroadcast
    {
        private readonly IProfileBroadcast origin;
        private readonly ISelfProfile selfProfile;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public EnsureSelfPublishedProfileBroadcast(IProfileBroadcast origin, ISelfProfile selfProfile)
        {
            this.origin = origin;
            this.selfProfile = selfProfile;
        }

        public void NotifyRemotes()
        {
            NotifyAsync(cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid NotifyAsync(CancellationToken ct)
        {
            bool published = await selfProfile.IsProfilePublishedAsync(ct);

            if (published == false)
                await selfProfile.PublishAsync(ct);

            origin.NotifyRemotes();
        }

        public void Dispose()
        {
            origin.Dispose();
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
