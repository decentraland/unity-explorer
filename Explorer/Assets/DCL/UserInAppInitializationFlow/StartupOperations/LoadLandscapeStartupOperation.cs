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

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LandscapeLoading);
            AsyncLoadProcessReport landscapeLoadReport = args.Report.CreateChildReport(finalizationProgress);
            await landscape.LoadTerrainAsync(landscapeLoadReport, ct);
            args.Report.SetProgress(finalizationProgress);
        }
    }
}
