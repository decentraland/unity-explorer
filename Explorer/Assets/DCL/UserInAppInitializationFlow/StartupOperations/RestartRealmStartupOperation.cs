using CommunicationData.URLHelpers;
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
        private readonly URLDomain defaultRealm;
        private bool reloadRealm;

        public RestartRealmStartupOperation(ILoadingStatus loadingStatus, IRealmController realmController, URLDomain defaultRealm)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.defaultRealm = defaultRealm;
        }

        public void EnableReload(bool enable)
        {
            reloadRealm = enable;
        }

        protected override async UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmRestarting);

            if (reloadRealm)
            {
                if (realmController.CurrentDomain.HasValue == false)
                    await realmController.SetRealmAsync(defaultRealm, ct);
                else
                    await realmController.RestartRealmAsync(ct);
            }

            report.SetProgress(finalizationProgress);
        }
    }
}
