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
            teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.Init);
            teleportParams.LoadingStatus.UpdateAssetsLoaded(0, 0);
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}