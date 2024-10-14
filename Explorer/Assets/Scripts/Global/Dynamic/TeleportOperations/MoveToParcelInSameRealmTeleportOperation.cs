using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.LoadingStatus.CompletedStage;


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
                var teleportLoadReport
                    = teleportParams.ParentReport.CreateChildReport(LoadingStatus.PROGRESS[PlayerTeleported]);
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.SceneLoading);
                var waitForSceneReadiness =
                    await realmNavigator.TeleportToParcelAsync(teleportParams.CurrentDestinationParcel,
                        teleportLoadReport, ct);
                await waitForSceneReadiness;
                teleportParams.ParentReport.SetProgress(teleportParams.LoadingStatus.SetCompletedStage(Completed));
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while teleporting in same realm");
            }
        }
    }
}
