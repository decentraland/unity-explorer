using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class RestartRealmStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IRealmController realmController;
        private bool reloadRealm;

        public RestartRealmStartupOperation(RealFlowLoadingStatus loadingStatus, IRealmController realmController)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
        }

        public void EnableReload(bool enable)
        {
            reloadRealm = enable;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            if (reloadRealm)
                await realmController.RestartRealmAsync(ct);

            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.ProfileLoaded));
            return Result.SuccessResult();
        }
    }
}
