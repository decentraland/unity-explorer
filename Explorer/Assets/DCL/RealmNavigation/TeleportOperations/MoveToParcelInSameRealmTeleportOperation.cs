using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInSameRealmTeleportOperation : ITeleportOperation
    {
        private readonly ITeleportController teleportController;

        public MoveToParcelInSameRealmTeleportOperation(ITeleportController teleportController)
        {
            this.teleportController = teleportController;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams args, CancellationToken ct)
        {
            float finalizationProgress = args.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = args.Report.CreateChildReport(finalizationProgress);
            EnumResult<TaskError> result = await teleportController.TryTeleportToSceneSpawnPointAsync(args.CurrentDestinationParcel, teleportLoadReport, ct);
            args.Report.SetProgress(finalizationProgress);
            return result;
        }
    }
}
