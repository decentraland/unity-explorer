using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class LoadLandscapeTeleportOperation : TeleportOperationBase
    {
        private readonly ILandscape landscape;

        public LoadLandscapeTeleportOperation(ILandscape landscape)
        {
            this.landscape = landscape;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress = teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LandscapeLoading);

            AsyncLoadProcessReport landscapeLoadReport
                = teleportParams.Report.CreateChildReport(finalizationProgress);

            await landscape.LoadTerrainAsync(landscapeLoadReport, ct);
            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
