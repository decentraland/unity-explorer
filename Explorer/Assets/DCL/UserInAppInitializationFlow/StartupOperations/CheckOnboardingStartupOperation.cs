using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class CheckOnboardingStartupOperation : IStartupOperation
    {
        private const int TUTORIAL_STEP_DONE_MARK = 256;
        private const string APP_PARAMETER_REALM = "realm";
        private const string APP_PARAMETER_LOCAL_SCENE = "local-scene";
        private const string APP_PARAMETER_POSITION = "position";

        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmController realmController;
        private readonly ISelfProfile selfProfile;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs appParameters;

        private Profile? ownProfile;
        private bool isProfilePendingToBeUpdated;

        public CheckOnboardingStartupOperation(
            ILoadingStatus loadingStatus,
            IRealmController realmController,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache,
            IDecentralandUrlsSource decentralandUrlsSource,
            IAppArgs appParameters)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.selfProfile = selfProfile;
            this.featureFlagsCache = featureFlagsCache;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appParameters = appParameters;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.OnboardingChecking);
            await CheckOnboardingAsync(ct);
            report.SetProgress(finalizationProgress);
            return Result.SuccessResult();
        }

        private async UniTask CheckOnboardingAsync(CancellationToken ct)
        {
            // It the app is open from any external way, we will ignore the onboarding flow
            if (appParameters.HasFlag(APP_PARAMETER_REALM) || appParameters.HasFlag(APP_PARAMETER_POSITION) || appParameters.HasFlag(APP_PARAMETER_LOCAL_SCENE))
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
                    await realmController.SetRealmAsync(URLDomain.FromString($"{IRealmNavigator.WORLDS_DOMAIN}/{realm}"), ct);
                    isProfilePendingToBeUpdated = true;
                }
                catch (Exception)
                {
                    // We redirect to Genesis City if the onboarding realm is not found
                    ReportHub.LogError(ReportCategory.ONBOARDING, $"Error trying to set '{realm}' realm for onboarding. Redirecting to Genesis City.");
                    await realmController.SetRealmAsync(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct);
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
