using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
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
            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
