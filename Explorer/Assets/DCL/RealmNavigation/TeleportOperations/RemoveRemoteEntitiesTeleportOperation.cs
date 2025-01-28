using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Entities;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class RemoveRemoteEntitiesTeleportOperation : TeleportOperationBase
    {
        private readonly IRemoteEntities remoteEntities;
        private readonly World globalWorld;

        public RemoveRemoteEntitiesTeleportOperation(IRemoteEntities remoteEntities, World globalWorld)
        {
            this.remoteEntities = remoteEntities;
            this.globalWorld = globalWorld;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            remoteEntities.ForceRemoveAll(globalWorld);
            return UniTask.CompletedTask;
        }
    }
}
