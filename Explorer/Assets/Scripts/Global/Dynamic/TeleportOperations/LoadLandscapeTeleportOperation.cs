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
        private readonly IRealmNavigator realmNavigator;

        public LoadLandscapeTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress = teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LandscapeLoading);

            AsyncLoadProcessReport landscapeLoadReport
                = teleportParams.ParentReport.CreateChildReport(finalizationProgress);

            await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
