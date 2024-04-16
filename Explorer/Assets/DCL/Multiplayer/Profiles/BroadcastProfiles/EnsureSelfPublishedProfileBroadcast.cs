using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;
using ECS;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class EnsureSelfPublishedProfileBroadcast : IProfileBroadcast
    {
        private readonly IProfileBroadcast origin;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realm;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public EnsureSelfPublishedProfileBroadcast(IProfileBroadcast origin, ISelfProfile selfProfile, IRealmData realm)
        {
            this.origin = origin;
            this.selfProfile = selfProfile;
            this.realm = realm;
        }

        public void NotifyRemotes()
        {
            NotifyAsync(cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid NotifyAsync(CancellationToken ct)
        {
            await realm.WaitConfiguredAsync();

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
