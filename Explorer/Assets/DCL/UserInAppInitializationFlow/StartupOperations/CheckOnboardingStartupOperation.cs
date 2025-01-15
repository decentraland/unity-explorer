using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class CheckOnboardingStartupOperation : StartUpOperationBase
    {
        private const int TUTORIAL_STEP_DONE_MARK = 256;


        private readonly ILoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs appParameters;
        private readonly IRealmNavigator realmNavigator;

        private Profile? ownProfile;
        private bool isProfilePendingToBeUpdated;

        public CheckOnboardingStartupOperation(
            ILoadingStatus loadingStatus,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache,
            IDecentralandUrlsSource decentralandUrlsSource,
            IAppArgs appParameters,
            IRealmNavigator realmNavigator)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
            this.featureFlagsCache = featureFlagsCache;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appParameters = appParameters;
            this.realmNavigator = realmNavigator;
        }

        protected override async UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.OnboardingChecking);
            await CheckOnboardingAsync(ct);
            report.SetProgress(finalizationProgress);
        }

        private async UniTask CheckOnboardingAsync(CancellationToken ct)
        {
            // It the app is open from any external way, we will ignore the onboarding flow
            if (appParameters.HasFlag(AppArgsFlags.REALM) || appParameters.HasFlag(AppArgsFlags.POSITION) || appParameters.HasFlag(AppArgsFlags.LOCAL_SCENE))
                return;

            isProfilePendingToBeUpdated = false;
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
                    URLDomain realmURL = URLDomain.FromString($"{IRealmNavigator.WORLDS_DOMAIN}/{realm}");
                    await realmNavigator.TryChangeRealmAsync(realmURL, ct);
                    isProfilePendingToBeUpdated = true;
                }
                catch (Exception)
                {
                    // We redirect to Genesis City if the onboarding realm is not found
                    ReportHub.LogError(ReportCategory.ONBOARDING, $"Error trying to set '{realm}' realm for onboarding. Redirecting to Genesis City.");
                    await realmNavigator.TryChangeRealmAsync(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct);
                }
            }
        }

        public async UniTask MarkOnboardingAsDoneAsync(World world, Entity playerEntity, CancellationToken ct)
        {
            if (!isProfilePendingToBeUpdated || ownProfile == null || ownProfile.TutorialStep > 0)
                return;

            try
            {
                // Update profile data
                ownProfile.TutorialStep = TUTORIAL_STEP_DONE_MARK;
                var profile = await selfProfile.ForcePublishWithoutModificationsAsync(ct);

                if (profile != null)
                {
                    // Update player entity in world
                    profile.IsDirty = true;
                    world.Set(playerEntity, profile);
                }

                isProfilePendingToBeUpdated = false;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.ONBOARDING, $"There was an error while trying to update TutorialStep into your profile. ERROR: {e.Message}");
            }
        }
    }
}
