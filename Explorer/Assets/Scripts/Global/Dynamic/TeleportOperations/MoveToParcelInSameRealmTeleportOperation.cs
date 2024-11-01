using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;


namespace Global.Dynamic.TeleportOperations
{
    public class MoveToParcelInSameRealmTeleportOperation : ITeleportOperation
    {
        private readonly IRealmNavigator realmNavigator;

        public MoveToParcelInSameRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                float finalizationProgress =
                    teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
                var teleportLoadReport
                    = teleportParams.ParentReport.CreateChildReport(finalizationProgress);
                var waitForSceneReadiness =
                    await realmNavigator.TeleportToParcelAsync(teleportParams.CurrentDestinationParcel,
                        teleportLoadReport, ct);
                await waitForSceneReadiness;
                teleportParams.ParentReport.SetProgress(finalizationProgress);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.REALM,
                    $"Teleport to parcel {teleportParams.CurrentDestinationParcel} in same realm failed with exception {e.Message}");
                return Result.ErrorResult("Error while teleporting in same realm");
            }
        }
    }
}
