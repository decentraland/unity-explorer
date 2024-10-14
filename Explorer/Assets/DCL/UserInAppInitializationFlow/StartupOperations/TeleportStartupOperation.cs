using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class TeleportStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmNavigator realmNavigator;
        private readonly Vector2Int startParcel;

        public TeleportStartupOperation(ILoadingStatus loadingStatus, IRealmNavigator realmNavigator, Vector2Int startParcel)
        {
            this.loadingStatus = loadingStatus;
            this.realmNavigator = realmNavigator;
            this.startParcel = startParcel;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            AsyncLoadProcessReport teleportLoadReport
                = report.CreateChildReport(LoadingStatus.PROGRESS[LoadingStatus.CompletedStage.PlayerTeleported]);

            loadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.SceneLoading);
            await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, startParcel);
            return Result.SuccessResult();
        }
    }
}
