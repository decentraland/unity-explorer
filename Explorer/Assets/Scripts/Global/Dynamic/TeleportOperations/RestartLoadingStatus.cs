using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RestartLoadingStatus : ITeleportOperation
    {
        public UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.ParentReport.SetProgress(teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init));
            teleportParams.LoadingStatus.UpdateAssetsLoaded(0, 0);
            return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }
}
