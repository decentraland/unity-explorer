using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks;
using DCL.RealmNavigation;
using DCL.UI;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IHealthCheck healthCheck;
        private readonly WarningNotificationView inWorldWarningNotificationView;


        public EnsureLivekitConnectionStartupOperation(ILoadingStatus loadingStatus, IHealthCheck healthCheck, WarningNotificationView inWorldWarningNotificationView)
        {
            this.loadingStatus = loadingStatus;
            this.healthCheck = healthCheck;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LiveKitConnectionEnsuring);
            report.Report.SetProgress(finalizationProgress);
            RunConnect(ct).Forget();
            return Result.SuccessResult().AsEnumResult(TaskError.MessageError);

        }

        private async UniTask RunConnect(CancellationToken ct)
        {
            Result result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (!result.Success)
            {
                inWorldWarningNotificationView.SetText("Couldnt connect to the multiplayer. You are on single player mode");
                inWorldWarningNotificationView.Show(ct);

                await UniTask.Delay(3000, cancellationToken: ct);

                inWorldWarningNotificationView.Hide(ct: ct);
            }
            else
            {
                inWorldWarningNotificationView.SetText("Connected to multiplayer");
                inWorldWarningNotificationView.Show(ct);

                await UniTask.Delay(3000, cancellationToken: ct);

                inWorldWarningNotificationView.Hide(ct: ct);
            }

        }
    }
}
