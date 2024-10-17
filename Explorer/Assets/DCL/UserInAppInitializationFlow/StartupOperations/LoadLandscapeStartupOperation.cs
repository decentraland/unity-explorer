using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

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

        public async UniTask<Result> ExecuteAsync(IAsyncLoadProcessReport report, CancellationToken ct)
        {
            IAsyncLoadProcessReport landscapeLoadReport
                = report.CreateChildReport(RealFlowLoadingStatus.PROGRESS[RealFlowLoadingStatus.Stage.LandscapeLoaded]);

            await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.LandscapeLoaded), "Landscape loaded");
            return Result.SuccessResult();
        }
    }
}
