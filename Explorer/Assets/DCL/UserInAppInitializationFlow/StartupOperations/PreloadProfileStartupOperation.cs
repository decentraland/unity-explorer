using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Profiles.Self;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class PreloadProfileStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;

        public PreloadProfileStartupOperation(RealFlowLoadingStatus loadingStatus, ISelfProfile selfProfile)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await selfProfile.ProfileOrPublishIfNotAsync(ct);
            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.ProfileLoaded));
            return StartupResult.SuccessResult();
        }
    }
}
