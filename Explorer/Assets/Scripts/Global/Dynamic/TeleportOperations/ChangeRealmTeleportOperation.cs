using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;


namespace Global.Dynamic.TeleportOperations
{
    public class ChangeRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmNavigator realmNavigator;

        public ChangeRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmChanging);

            await realmNavigator.ChangeRealmAsync(teleportParams.CurrentDestinationRealm, ct);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
