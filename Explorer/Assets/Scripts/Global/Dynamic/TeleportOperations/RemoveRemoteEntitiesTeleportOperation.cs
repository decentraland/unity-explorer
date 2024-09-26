using System;
using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Entities;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RemoveRemoteEntitiesTeleportOperation : ITeleportOperation
    {
        private readonly IRemoteEntities remoteEntities;
        private readonly World globalWorld;


        public RemoveRemoteEntitiesTeleportOperation(IRemoteEntities remoteEntities, World globalWorld)
        {
            this.remoteEntities = remoteEntities;
            this.globalWorld = globalWorld;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                remoteEntities.ForceRemoveAll(globalWorld);
                return UniTask.FromResult(Result.SuccessResult());
            }
            catch (Exception e)
            {
                return UniTask.FromResult(Result.ErrorResult("Failed to remove remote entities"));
            }
        }
    }
}