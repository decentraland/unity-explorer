using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using Utility.Types;
using static DCL.UserInAppInitializationFlow.LoadingStatus.CompletedStage;


namespace Global.Dynamic.TeleportOperations
{
    public class LoadLandscapeTeleportOperation : ITeleportOperation
    {
        private readonly IRealmNavigator realmNavigator;

        public LoadLandscapeTeleportOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }


        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                var landscapeLoadReport
                    = teleportParams.ParentReport.CreateChildReport(LoadingStatus.PROGRESS[LandscapeLoaded]);
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.LandscapeLoading);
                await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
                teleportParams.ParentReport.SetProgress(teleportParams.LoadingStatus.SetCompletedStage(LandscapeLoaded));
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Error while loading landscape");
            }
        }
    }
}