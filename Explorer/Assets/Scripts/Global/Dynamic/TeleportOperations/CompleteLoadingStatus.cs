using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class CompleteLoadingStatus : ITeleportOperation
    {
        private readonly TeleportCounter teleportCounter;
        private readonly bool goingToNewRealm;

        public CompleteLoadingStatus(TeleportCounter teleportCounter, bool goingToNewRealm)
        {
            this.teleportCounter = teleportCounter;
            this.goingToNewRealm = goingToNewRealm;
        }
        
        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            teleportParams.ParentReport.SetProgress(teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
            teleportCounter.AddSuccessfullTeleport(teleportParams.CurrentDestinationParcel,
                teleportParams.CurrentDestinationRealm, goingToNewRealm);
            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}