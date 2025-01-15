using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class LoadLandscapeStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly ILandscape landscape;

        public LoadLandscapeStartupOperation(ILoadingStatus loadingStatus, ILandscape landscape)
        {
            this.loadingStatus = loadingStatus;
            this.landscape = landscape;
        }

        protected override async UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LandscapeLoading);
            AsyncLoadProcessReport landscapeLoadReport = report.CreateChildReport(finalizationProgress);
            await landscape.LoadTerrainAsync(landscapeLoadReport, ct);
            report.SetProgress(finalizationProgress);
        }
    }
}
