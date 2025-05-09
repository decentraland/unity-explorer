using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
    public class CheckOnboardingStartupOperation : IStartupOperation
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

        public async UniTask MarkOnboardingAsDoneAsync(World world, Entity playerEntity, CancellationToken ct)
        {
            if (!isProfilePendingToBeUpdated || ownProfile == null || ownProfile.TutorialStep > 0)
                return;

            try
            {
                // Update profile data
                ownProfile.TutorialStep = TUTORIAL_STEP_DONE_MARK;
                Profile? profile = await selfProfile.ForcePublishWithoutModificationsAsync(ct);

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

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.OnboardingChecking);
            EnumResult<TaskError> res = await TryToChangeToOnBoardingRealm(ct);

            if (!res.Success)
                return res;

            args.Report.SetProgress(finalizationProgress);
            return res;
        }

        private async UniTask<EnumResult<TaskError>> TryToChangeToOnBoardingRealm(CancellationToken ct)
        {
            // It the app is open from any external way, we will ignore the onboarding flow
            if (appParameters.HasFlag(AppArgsFlags.REALM) || appParameters.HasFlag(AppArgsFlags.POSITION) || appParameters.HasFlag(AppArgsFlags.LOCAL_SCENE))
                return EnumResult<TaskError>.SuccessResult();

            isProfilePendingToBeUpdated = false;
            ownProfile = await selfProfile.ProfileAsync(ct);

            // If the user has already completed the tutorial, we don't need to check the onboarding realm
            if (ownProfile is { TutorialStep: > 0 })
                return EnumResult<TaskError>.SuccessResult();

            if (!TrySolveRealmFromFeatureFlags(out string? realm))
                return EnumResult<TaskError>.SuccessResult();

            // If the onboarding feature flag is enabled, we set the realm to the onboarding realm
            // TODO the following flow is suspicious: realmNavigator itself is wrapped in the loading screen, and this operation is a part of the loading screen,
            // TODO So operations will be called from the operation. Re-consideration required
            //try
            //{
            string worldContentServerUrl = decentralandUrlsSource.Url(DecentralandUrl.WorldContentServer);
            var realmURL = URLDomain.FromString($"{worldContentServerUrl}/{realm}");
            EnumResult<ChangeRealmError> result = await realmNavigator.TryChangeRealmAsync(realmURL, ct);
            isProfilePendingToBeUpdated = true;

            if (result.Success)
                return EnumResult<TaskError>.SuccessResult();

            if (result.Error!.Value.State is ChangeRealmError.MessageError or ChangeRealmError.NotReachable)
                return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, result.Error.Value.Message);

            //}
            // RealmNavigator already contains fallback logic to the previously loaded realm
            // catch (Exception)
            // {
            //     // We redirect to Genesis City if the onboarding realm is not found
            //     ReportHub.LogError(ReportCategory.ONBOARDING, $"Error trying to set '{realm}' realm for onboarding. Redirecting to Genesis City.");
            //     await realmNavigator.TryChangeRealmAsync(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct);
            // }

            return EnumResult<TaskError>.SuccessResult();
        }

        private bool TrySolveRealmFromFeatureFlags(out string? realm)
        {
            realm = null;

            if (!featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.ONBOARDING))
                return false;

            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT))
            {
                if (!featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT, out realm))
                    return false;

                return !string.IsNullOrEmpty(realm);
            }

            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT))
            {
                if (!featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.ONBOARDING, FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT, out realm))
                    return false;

                return !string.IsNullOrEmpty(realm);
            }

            return false;
        }
    }
}
