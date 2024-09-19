using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS.SceneLifeCycle.Realm;
using System;
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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private Profile? ownProfile;

        public CheckOnboardingStartupOperation(
            RealFlowLoadingStatus loadingStatus,
            IRealmController realmController,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.selfProfile = selfProfile;
            this.featureFlagsCache = featureFlagsCache;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await CheckOnboardingAsync(ct);

            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.OnboardingChecked));
            return Result.SuccessResult();
        }

        private async UniTask CheckOnboardingAsync(CancellationToken ct)
        {
            ownProfile = await selfProfile.ProfileAsync(ct);

            // If the user has already completed the tutorial, we don't need to check the onboarding realm
            if (ownProfile is { TutorialStep: > 0 } )
                return;

            // If the onboarding feature flag is enabled, we set the realm to the onboarding realm
            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT))
            {
                if (!featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT, out string? realm))
                    return;

                if (string.IsNullOrEmpty(realm))
                    return;

                try
                {
                    await realmController.SetRealmAsync(URLDomain.FromString($"{IRealmNavigator.WORLDS_DOMAIN}/{realm}"), ct);
                }
                catch (Exception)
                {
                    // We redirect to Genesis City if the onboarding realm is not found
                    ReportHub.LogError(ReportCategory.ONBOARDING, $"Error trying to set '{realm}' realm for onboarding. Redirecting to Genesis City.");
                    await realmController.SetRealmAsync(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct);
                }
            }
        }
    }
}
