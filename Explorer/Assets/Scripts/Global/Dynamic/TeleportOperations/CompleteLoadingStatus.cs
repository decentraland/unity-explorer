using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class CompleteLoadingStatus : ITeleportOperation
    {
        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.ParentReport.SetProgress(teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}