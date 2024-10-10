using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RestartLoadingStatus : ITeleportOperation
    {
        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.RealFlowLoadingStatus.SetCompletedStage(LoadingStatus.Stage.Init);
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}