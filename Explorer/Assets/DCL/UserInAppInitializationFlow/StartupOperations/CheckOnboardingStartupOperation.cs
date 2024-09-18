using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.FeatureFlags;
using DCL.Profiles.Self;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class CheckOnboardingStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IRealmController realmController;
        private readonly ISelfProfile selfProfile;
        private readonly FeatureFlagsCache featureFlagsCache;

        public CheckOnboardingStartupOperation(
            RealFlowLoadingStatus loadingStatus,
            IRealmController realmController,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.selfProfile = selfProfile;
            this.featureFlagsCache = featureFlagsCache;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await CheckOnboardingAsync(ct);

            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.OnboardingChecked));
            return Result.SuccessResult();
        }

        private async UniTask CheckOnboardingAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            if (profile is { TutorialStep: > 0 } )
                return;

            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT))
            {
                if (!featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT, out string? realm))
                    return;

                if (string.IsNullOrEmpty(realm))
                    return;

                await realmController.SetRealmAsync(URLDomain.FromString($"{IRealmNavigator.WORLDS_DOMAIN}/{realm}"), ct);
            }
        }
    }
}
