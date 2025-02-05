using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
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
            await selfProfile.ProfileOrPublishIfNotAsync(ct);
            args.Report.SetProgress(finalizationProgress);
        }
    }
}
