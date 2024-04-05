using Cysharp.Threading.Tasks;
using DCL.Profiles.Publishing;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class EnsureSelfPublishedProfileBroadcast : IProfileBroadcast
    {
        private readonly IProfileBroadcast origin;
        private readonly IProfilePublishing profilePublishing;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public EnsureSelfPublishedProfileBroadcast(IProfileBroadcast origin, IProfilePublishing profilePublishing)
        {
            this.origin = origin;
            this.profilePublishing = profilePublishing;
        }

        public void NotifyRemotes()
        {
            NotifyAsync(cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid NotifyAsync(CancellationToken ct)
        {
            bool published = await profilePublishing.IsProfilePublishedAsync(ct);

            if (published == false)
                await profilePublishing.PublishProfileAsync(ct);

            origin.NotifyRemotes();
        }

        public void Dispose()
        {
            origin.Dispose();
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
