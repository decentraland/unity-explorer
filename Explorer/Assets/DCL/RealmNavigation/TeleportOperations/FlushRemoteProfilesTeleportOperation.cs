using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    // Stale profiles from the previous realm would otherwise materialize as avatars for players absent in the new one.
    public class FlushRemoteProfilesTeleportOperation : TeleportOperationBase
    {
        private readonly RemoteProfiles remoteProfiles;
        private readonly IRemoteAnnouncements remoteAnnouncements;

        public FlushRemoteProfilesTeleportOperation(RemoteProfiles remoteProfiles, IRemoteAnnouncements remoteAnnouncements)
        {
            this.remoteProfiles = remoteProfiles;
            this.remoteAnnouncements = remoteAnnouncements;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            remoteAnnouncements.Clear();
            remoteProfiles.Reset();
            return UniTask.CompletedTask;
        }
    }
}
