using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.Utilities;
using ECS.Prioritization.Components;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class TeleportStartupOperation : TeleportToSpawnPointOperationBase<IStartupOperation.Params>, IStartupOperation
    {
        private readonly StartParcel startParcel;

        public TeleportStartupOperation(
            ILoadingStatus loadingStatus,
            IGlobalRealmController realmController,
            ObjectProxy<Entity> cameraEntity,
            ITeleportController teleportController,
            CameraSamplingData cameraSamplingData, StartParcel startParcel, string reportCategory = ReportCategory.SCENE_LOADING)
            : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory)
        {
            this.startParcel = startParcel;
        }

        public override UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct) =>
            InternalExecuteAsync(args, startParcel.ConsumeByTeleportOperation(), ct);
    }
}
