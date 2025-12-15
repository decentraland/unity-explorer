using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class CheckOnboardingStartupOperation
    {
        private const int TUTORIAL_STEP_DONE_MARK = 256;

        private readonly ILoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;

        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs appParameters;
        private readonly IGlobalRealmController realmController;

        private Profile? ownProfile;
        private bool isProfilePendingToBeUpdated;

        public CheckOnboardingStartupOperation(
            ILoadingStatus loadingStatus,
            ISelfProfile selfProfile,
            IDecentralandUrlsSource decentralandUrlsSource,
            IAppArgs appParameters,
            IGlobalRealmController realmController)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appParameters = appParameters;
            this.realmController = realmController;
        }

        public async UniTask MarkOnboardingAsDoneAsync(World world, Entity playerEntity, CancellationToken ct)
        {
            if (!isProfilePendingToBeUpdated || ownProfile == null || ownProfile.TutorialStep > 0)
                return;

            try
            {
                // Update profile data
                ownProfile.TutorialStep = TUTORIAL_STEP_DONE_MARK;

                Profile? profile = await selfProfile.UpdateProfileAsync(ownProfile, ct,
                    // No need to update avatar, since we only modify the tutorial step, not wearables nor emotes
                    updateAvatarInWorld: false);

                if (profile != null)
                {
                    // Update player entity in world
                    profile.IsDirty = true;
                    world.Set(playerEntity, profile);
                }

                isProfilePendingToBeUpdated = false;
            }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogError(ReportCategory.ONBOARDING, $"There was an error while trying to update TutorialStep into your profile. ERROR: {e.Message}"); }
        }

        public async UniTask ExecuteAsync(CancellationToken ct) =>
            await TryToChangeToOnBoardingRealmAsync(ct);

        private async UniTask TryToChangeToOnBoardingRealmAsync(CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.OnboardingChecking);
            // It the app is open from any external way, we will ignore the onboarding flow
            if (appParameters.HasFlag(AppArgsFlags.REALM) || appParameters.HasFlag(AppArgsFlags.POSITION) || appParameters.HasFlag(AppArgsFlags.LOCAL_SCENE) || appParameters.HasFlag(AppArgsFlags.COMMUNITY))
                return;

            isProfilePendingToBeUpdated = false;
            ownProfile = await selfProfile.ProfileAsync(ct);

            // If the user has already completed the tutorial, we don't need to check the onboarding realm
            if (ownProfile is { TutorialStep: > 0 })
                return;

            // TODO: Remove the greeting-onboarding ff when it is finally moved to production. Keep onboarding only.
            //.We use the greeting-onboarding FF so we are able to test it on dev environment.
            if (!TrySolveRealmFromFeatureFlags(FeatureFlagsStrings.GREETING_ONBOARDING, out string? realm))
                if (!TrySolveRealmFromFeatureFlags(FeatureFlagsStrings.ONBOARDING, out realm))
                    return;

            string worldContentServerUrl = decentralandUrlsSource.Url(DecentralandUrl.WorldContentServer);
            var realmURL = URLDomain.FromString($"{worldContentServerUrl}/{realm}");

            if (await realmController.IsReachableAsync(realmURL, ct))
                await realmController.SetRealmAsync(realmURL, ct);
            else
                ReportHub.LogError(ReportCategory.ONBOARDING, $"Error trying to set '{realm}' realm for onboarding. Redirecting to Genesis City.");

            isProfilePendingToBeUpdated = true;
        }

        private bool TrySolveRealmFromFeatureFlags(string featureFlag, out string? realm)
        {
            realm = null;

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            if (!featureFlags.IsEnabled(featureFlag))
                return false;

            if (featureFlags.IsEnabled(featureFlag, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT))
            {
                if (!featureFlags.TryGetTextPayload(featureFlag, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT, out realm))
                    return false;

                return !string.IsNullOrEmpty(realm);
            }

            if (featureFlags.IsEnabled(featureFlag, FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT))
            {
                if (!featureFlags.TryGetTextPayload(featureFlag, FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT, out realm))
                    return false;

                return !string.IsNullOrEmpty(realm);
            }

            return false;
        }
    }
}
