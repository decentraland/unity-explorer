using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;


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
                float finalizationProgress =
                    teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmRestarting);
                await realmNavigator.ChangeRealmAsync(teleportParams.CurrentDestinationRealm, ct);
                teleportParams.ParentReport.SetProgress(finalizationProgress);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while changing realm");
            }
        }
    }
}