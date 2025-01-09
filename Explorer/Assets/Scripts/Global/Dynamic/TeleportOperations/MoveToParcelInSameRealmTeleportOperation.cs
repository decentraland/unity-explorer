using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.ParcelsService;
using DCL.UserInAppInitializationFlow;

namespace Global.Dynamic.TeleportOperations
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
            AsyncLoadProcessReport teleportLoadReport = teleportParams.ParentReport.CreateChildReport(finalizationProgress);
            await teleportController.TryTeleportToSceneSpawnPointAsync(teleportParams.CurrentDestinationParcel, teleportLoadReport, ct);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
