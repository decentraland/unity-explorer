using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class TeleportStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;
        private readonly Vector2Int startParcel;

        public TeleportStartupOperation(RealFlowLoadingStatus loadingStatus, IRealmNavigator realmNavigator, Vector2Int startParcel)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
            this.startParcel = startParcel;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            AsyncLoadProcessReport teleportLoadReport
                = report.CreateChildReport(RealFlowLoadingStatus.PROGRESS[RealFlowLoadingStatus.Stage.PlayerTeleported]);

            await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, startParcel);
            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.Completed));
            return StartupResult.SuccessResult();
        }
    }
}
