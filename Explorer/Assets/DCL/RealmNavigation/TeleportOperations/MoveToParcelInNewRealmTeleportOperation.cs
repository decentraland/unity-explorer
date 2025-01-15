using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ParcelsService;
using DCL.Utilities;
using ECS.Prioritization.Components;
using Global.Dynamic;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInNewRealmTeleportOperation : TeleportToSpawnPointOperationBase<TeleportParams>, ITeleportOperation
    {
        public MoveToParcelInNewRealmTeleportOperation(ILoadingStatus loadingStatus, IGlobalRealmController realmController, ObjectProxy<Entity> cameraEntity, ITeleportController teleportController, CameraSamplingData cameraSamplingData,
            string reportCategory = ReportCategory.SCENE_LOADING) : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory) { }

        protected override UniTask InternalExecuteAsync(TeleportParams args, CancellationToken ct) =>
            InternalExecuteAsync(args, args.CurrentDestinationParcel, ct);
    }
}
