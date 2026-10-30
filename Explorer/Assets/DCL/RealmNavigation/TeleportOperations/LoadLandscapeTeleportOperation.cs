using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using System;
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

            EnumResult<LandscapeError> result = await landscape.LoadTerrainAsync(landscapeLoadReport, ct);

            // LandscapeDisabled is a valid configuration; any other failure means the terrain is not loaded
            // and must fail the teleport instead of reporting success without ground
            if (!result.Success && result.Error!.Value.State != LandscapeError.LandscapeDisabled)
            {
                ct.ThrowIfCancellationRequested();
                throw new Exception($"Landscape loading failed: {result.Error.AsMessage()}");
            }

            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
