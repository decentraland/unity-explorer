using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SwitchRealmMiscVisibilityStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmController realmController;
        private readonly IRealmMisc realmMisc;

        public SwitchRealmMiscVisibilityStartupOperation(ILoadingStatus loadingStatus, IRealmController realmController, IRealmMisc realmMisc)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.realmMisc = realmMisc;
        }

        protected override UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.EnvironmentMiscSetting);
            realmMisc.SwitchTo(realmController.Type);
            report.SetProgress(finalizationProgress);
            return UniTask.CompletedTask;
        }
    }
}
