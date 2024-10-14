using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class LoadLandscapeStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;

        public LoadLandscapeStartupOperation(ILoadingStatus loadingStatus, IRealmNavigator realmNavigator)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            AsyncLoadProcessReport landscapeLoadReport
                = report.CreateChildReport(LoadingStatus.PROGRESS[LoadingStatus.CompletedStage.LandscapeLoaded]);
            loadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.LandscapeLoading);
            await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
            report.SetProgress(loadingStatus.SetCompletedStage(LoadingStatus.CompletedStage.LandscapeLoaded));
            return Result.SuccessResult();
        }
    }
}
