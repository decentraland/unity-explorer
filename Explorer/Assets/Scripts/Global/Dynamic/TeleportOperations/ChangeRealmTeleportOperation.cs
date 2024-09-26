using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;


namespace Global.Dynamic.TeleportOperations
{
    public class ChangeRealmTeleportOperation : ITeleportOperation
    {
        private readonly IRealmNavigator realmNavigator;

        public ChangeRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                await realmNavigator.ChangeRealmAsync(teleportParams.CurrentDestinationRealm, ct);
                teleportParams.ParentReport.SetProgress(RealFlowLoadingStatus.PROGRESS[ProfileLoaded]);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while changing realm");
            }
        }
    }
}