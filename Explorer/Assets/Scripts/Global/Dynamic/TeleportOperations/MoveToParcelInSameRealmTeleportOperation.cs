using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class MoveToParcelInSameRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmNavigator realmNavigator;

        public MoveToParcelInSameRealmTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);

            AsyncLoadProcessReport teleportLoadReport
                = teleportParams.ParentReport.CreateChildReport(finalizationProgress);

            UniTask waitForSceneReadiness =
                await realmNavigator.TeleportToParcelAsync(teleportParams.CurrentDestinationParcel,
                    teleportLoadReport, ct);

            await waitForSceneReadiness;
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
