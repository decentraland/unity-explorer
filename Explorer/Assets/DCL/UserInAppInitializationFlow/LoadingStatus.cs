using DCL.Utilities;
using System.Collections.Generic;
using DCL.DebugUtilities.UIBindings;
using Utility;

namespace DCL.UserInAppInitializationFlow
{
    public class LoadingStatus : ILoadingStatus
    {
        public ReactiveProperty<CompletedStage> CurrentCompletedStage { get; } = new (CompletedStage.Init);
        public ElementBinding<string> CurrentStageBinding { get;  } = new (string.Empty);
        public ElementBinding<string> CurrentAssetsToLoad { get; } = new (string.Empty);
        public ElementBinding<string> CurrentAssetsLoaded { get; } = new (string.Empty);


        public enum CompletedStage : byte
        {
            Init = 0,
            AuthenticationScreenShown = 1,
            LiveKitConnectionEnsured = 2,
            FeatureFlagInitialized = 3,
            ProfileLoaded = 4,
            EnvironmentMiscSet = 5,
            PlayerAvatarLoaded = 6,
            LandscapeLoaded = 7,
            OnboardingChecked = 8,
            RealmRestarted = 9,

            /// <summary>
            ///     Player has teleported to the spawn point of the starting scene
            /// </summary>
            PlayerTeleported = 10,
            GlobalPXsLoaded = 11,
            Completed = 12,
        }

        public static readonly Dictionary<CompletedStage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<CompletedStage>())
        {
            [CompletedStage.Init] = 0f, [CompletedStage.AuthenticationScreenShown] = 0.05f, [CompletedStage.LiveKitConnectionEnsured] = 0.1f, [CompletedStage.FeatureFlagInitialized] = 0.15f,
            [CompletedStage.ProfileLoaded] = 0.2f, [CompletedStage.EnvironmentMiscSet] = 0.25f, [CompletedStage.PlayerAvatarLoaded] = 0.4f, [CompletedStage.LandscapeLoaded] = 0.7f,
            [CompletedStage.OnboardingChecked] = 0.80f, [CompletedStage.RealmRestarted] = 0.85f, [CompletedStage.PlayerTeleported] = 0.95f, [CompletedStage.GlobalPXsLoaded] = 0.99f,
            [CompletedStage.Completed] = 1f
        };


        public enum CurrentStage : byte
        {
            Init,
            LivekitStopping,
            RealmChanging,
            LandscapeLoading,
            SceneLoading,
            LivekitRestarting,
            ProfileLoading,
            OnboardingChecking,
            EnsuringLiveKitConnection,
            FeatureFlagInitializing,
            GlobalPXsLoading,
            PlayerAvatarLoading,
            EnvironmentMiscSetting,
            Done
        }


        public void SetCurrentStage(CurrentStage stage)
        {
            CurrentStageBinding.Value = stage.ToString();
        }

        public float SetCompletedStage(CompletedStage stage)
        {
            CurrentCompletedStage.Value = stage;
            return PROGRESS[stage];
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            CurrentAssetsToLoad.Value = assetsToLoad.ToString();
            CurrentAssetsLoaded.Value = assetsLoaded.ToString();
        }
    }
}
