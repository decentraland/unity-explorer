using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Profiles.Self;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class PreloadProfileStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;

        public PreloadProfileStartupOperation(ILoadingStatus loadingStatus, ISelfProfile selfProfile)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.ProfileLoading);
            await selfProfile.ProfileOrPublishIfNotAsync(ct);
            report.SetProgress(finalizationProgress);
            return Result.SuccessResult();
        }
    }
}
