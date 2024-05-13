using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using ECS;
using System;
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
            try
            {
                await realm.WaitConfiguredAsync();

                bool published = await selfProfile.IsProfilePublishedAsync(ct);

                if (published == false)
                    await selfProfile.PublishAsync(ct);

                origin.NotifyRemotes();
            }
            catch (OperationCanceledException) { }
            // It can happen, for example when the user performs a logout, that the web3 identity becomes invalid
            catch (Web3IdentityMissingException)
            {
                ReportHub.LogError(new ReportData(ReportCategory.MULTIPLAYER), "Cannot broadcast the self profile because web3 identity is invalid");
            }
        }

        public void Dispose()
        {
            origin.Dispose();
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
