using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class LoadLandscapeStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;

        public LoadLandscapeStartupOperation(RealFlowLoadingStatus loadingStatus, IRealmNavigator realmNavigator)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            AsyncLoadProcessReport landscapeLoadReport
                = report.CreateChildReport(RealFlowLoadingStatus.PROGRESS[RealFlowLoadingStatus.Stage.LandscapeLoaded]);

            await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.LandscapeLoaded));
            return StartupResult.SuccessResult();
        }
    }
}
