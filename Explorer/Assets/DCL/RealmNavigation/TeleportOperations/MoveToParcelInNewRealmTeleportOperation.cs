using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.Prioritization.Components;
using Global.Dynamic;
using System;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInNewRealmTeleportOperation : TeleportToSpawnPointOperationBase<TeleportParams>, ITeleportOperation
    {
        public MoveToParcelInNewRealmTeleportOperation(ILoadingStatus loadingStatus, IGlobalRealmController realmController, ObjectProxy<Entity> cameraEntity, ITeleportController teleportController, CameraSamplingData cameraSamplingData,
            string reportCategory = ReportCategory.SCENE_LOADING) : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory) { }

        public override UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams args, CancellationToken ct) =>
            InternalExecuteAsync(args, args.CurrentDestinationParcel, ct);
    }
}
