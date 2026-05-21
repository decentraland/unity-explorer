using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class RemoveRemoteEntitiesTeleportOperation : TeleportOperationBase
    {
        private readonly IRemoteEntities remoteEntities;
        private readonly RemoteAnnouncements remoteAnnouncements;
        private readonly RemoteProfiles remoteProfiles;
        private readonly World globalWorld;

        public RemoveRemoteEntitiesTeleportOperation(IRemoteEntities remoteEntities, RemoteAnnouncements remoteAnnouncements, RemoteProfiles remoteProfiles, World globalWorld)
        {
            this.remoteEntities = remoteEntities;
            this.remoteAnnouncements = remoteAnnouncements;
            this.remoteProfiles = remoteProfiles;
            this.globalWorld = globalWorld;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            // Drop in-flight profile downloads and queued announcements from the previous realm so they don't appear as ghosts
            remoteAnnouncements.Reset();
            remoteProfiles.Reset();
            remoteEntities.ForceRemoveAll(globalWorld);
            return UniTask.CompletedTask;
        }
    }
}
