using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.LoadingStatus.Stage;


namespace Global.Dynamic.TeleportOperations
{
    public class MoveToParcelInNewRealmTeleportOperation : ITeleportOperation
    {
        private readonly IRealmNavigator realmNavigator;

        public MoveToParcelInNewRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                var teleportLoadReport
                    = teleportParams.ParentReport.CreateChildReport(LoadingStatus.PROGRESS[PlayerTeleported]);

                await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct,
                    teleportParams.CurrentDestinationParcel);

                teleportParams.ParentReport.SetProgress(teleportParams.RealFlowLoadingStatus.SetCompletedStage(PlayerTeleported));
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while moving to parcel");
            }
        }
    }
}