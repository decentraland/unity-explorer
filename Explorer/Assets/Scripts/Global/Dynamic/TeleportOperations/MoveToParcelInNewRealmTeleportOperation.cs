using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;

namespace Global.Dynamic.TeleportOperations
{
    public class MoveToParcelInNewRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmNavigator realmNavigator;

        public MoveToParcelInNewRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress = teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);

            AsyncLoadProcessReport teleportLoadReport
                = teleportParams.ParentReport.CreateChildReport(finalizationProgress);

            await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct,
                teleportParams.CurrentDestinationParcel);

            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
