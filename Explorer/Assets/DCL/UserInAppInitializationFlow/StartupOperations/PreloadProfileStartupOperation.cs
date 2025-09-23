using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class PreloadProfileStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;

        public PreloadProfileStartupOperation(ILoadingStatus loadingStatus, ISelfProfile selfProfile)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.ProfileLoading);
            await selfProfile.ProfileAsync(ct);
            args.Report.SetProgress(finalizationProgress);
        }
    }
}
