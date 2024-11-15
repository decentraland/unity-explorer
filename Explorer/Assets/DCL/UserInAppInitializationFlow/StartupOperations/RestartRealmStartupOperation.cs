using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class RestartRealmStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmController realmController;
        private bool reloadRealm;

        public RestartRealmStartupOperation(ILoadingStatus loadingStatus, IRealmController realmController)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
        }

        public void EnableReload(bool enable)
        {
            reloadRealm = enable;
        }

        protected override async UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmRestarting);
            if (reloadRealm)
                await realmController.RestartRealmAsync(ct);
            report.SetProgress(finalizationProgress);
        }
    }
}
