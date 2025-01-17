using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInSameRealmTeleportOperation : TeleportOperationBase
    {
        private readonly ITeleportController teleportController;

        public MoveToParcelInSameRealmTeleportOperation(ITeleportController teleportController)
        {
            this.teleportController = teleportController;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress = teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = teleportParams.Report.CreateChildReport(finalizationProgress);
            await teleportController.TryTeleportToSceneSpawnPointAsync(teleportParams.CurrentDestinationParcel, teleportLoadReport, ct);
            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
