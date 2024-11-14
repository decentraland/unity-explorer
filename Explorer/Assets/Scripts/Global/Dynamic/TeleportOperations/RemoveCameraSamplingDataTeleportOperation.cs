using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.Prioritization.Components;
using System;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RemoveCameraSamplingDataTeleportOperation : TeleportOperationBase
    {
        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntity;

        public RemoveCameraSamplingDataTeleportOperation(World globalWorld, ObjectProxy<Entity> cameraEntity)
        {
            this.globalWorld = globalWorld;
            this.cameraEntity = cameraEntity;
        }

        protected override UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            // By removing the CameraSamplingData, we stop the ring calculation
            globalWorld.Remove<CameraSamplingData>(cameraEntity.Object);
            return UniTask.CompletedTask;
        }
    }
}
