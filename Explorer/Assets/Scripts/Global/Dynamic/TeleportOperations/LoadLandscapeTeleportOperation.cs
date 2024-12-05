using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
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
                = teleportParams.ParentReport.CreateChildReport(finalizationProgress);

            await landscape.LoadTerrainAsync(landscapeLoadReport, ct);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
