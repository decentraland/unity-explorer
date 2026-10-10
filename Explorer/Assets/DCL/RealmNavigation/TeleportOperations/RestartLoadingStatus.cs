using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class RestartLoadingStatus : ITeleportOperation
    {
        public UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.Report.SetProgress(teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init));
            teleportParams.LoadingStatus.UpdateAssetsLoaded(0, 0);
            return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }
}
