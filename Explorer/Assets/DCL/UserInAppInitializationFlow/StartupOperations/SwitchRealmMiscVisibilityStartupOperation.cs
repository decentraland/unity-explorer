using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SwitchRealmMiscVisibilityStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;

        public SwitchRealmMiscVisibilityStartupOperation(ILoadingStatus loadingStatus, IRealmNavigator realmNavigator)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.EnvironmentMiscSetting);
            realmNavigator.SwitchMiscVisibilityAsync();
            report.SetProgress(finalizationProgress);
        }
    }
}
