using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SwitchRealmMiscVisibilityStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;

        public SwitchRealmMiscVisibilityStartupOperation(RealFlowLoadingStatus loadingStatus, IRealmNavigator realmNavigator)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await realmNavigator.SwitchMiscVisibilityAsync();
            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.EnvironmentMiscSet));
            return Result.SuccessResult();
        }
    }
}
