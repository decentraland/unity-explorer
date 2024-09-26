using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.Prioritization.Components;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RemoveCameraSamplingDataTeleportOperation : ITeleportOperation
    {
        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntity;

        public RemoveCameraSamplingDataTeleportOperation(World globalWorld, ObjectProxy<Entity> cameraEntity)
        {
            this.globalWorld = globalWorld;
            this.cameraEntity = cameraEntity;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            // By removing the CameraSamplingData, we stop the ring calculation
            globalWorld.Remove<CameraSamplingData>(cameraEntity.Object);
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}