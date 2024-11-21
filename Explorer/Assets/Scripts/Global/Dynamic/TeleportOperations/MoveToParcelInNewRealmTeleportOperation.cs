using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;

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
                float finalizationProgress = teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
                AsyncLoadProcessReport teleportLoadReport = teleportParams.ParentReport.CreateChildReport(finalizationProgress);
                await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, teleportParams.CurrentDestinationParcel);
                teleportParams.ParentReport.SetProgress(finalizationProgress);
                return Result.SuccessResult();
            }
            catch (Exception e) { return Result.ErrorResult("Error while moving to parcel"); }
        }
    }
}
