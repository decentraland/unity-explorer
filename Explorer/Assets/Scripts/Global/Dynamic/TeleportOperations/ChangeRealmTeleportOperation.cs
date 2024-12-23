using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic.Misc;

namespace Global.Dynamic.TeleportOperations
{
    public class ChangeRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmController realmController;
        private readonly IRealmMisc realmMisc;

        public ChangeRealmTeleportOperation(IRealmController realmController, IRealmMisc realmMisc)
        {
            this.realmMisc = realmMisc;
            this.realmController = realmController;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmChanging);

            await realmController.SetRealmAsync(teleportParams.CurrentDestinationRealm, ct);
            realmMisc.SwitchTo(realmController.Type);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
