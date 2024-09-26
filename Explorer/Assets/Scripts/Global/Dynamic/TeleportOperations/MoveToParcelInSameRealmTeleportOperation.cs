using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;


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
                    = teleportParams.ParentReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);
                var waitForSceneReadiness =
                    await realmNavigator.TeleportToParcelAsync(teleportParams.CurrentDestinationParcel,
                        teleportLoadReport, ct);
                await waitForSceneReadiness;
                teleportParams.ParentReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while teleporting in same realm");
            }
        }
    }
}