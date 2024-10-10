using DCL.Utilities;
using System.Collections.Generic;
using DCL.DebugUtilities.UIBindings;
using Utility;

namespace DCL.UserInAppInitializationFlow
{
    public class LoadingStatus : ILoadingStatus
    {
        public ReactiveProperty<Stage> CurrentCompletedStage { get; } = new (Stage.Init);
        public ElementBinding<string> CurrentCompletedStageBinding { get;  } = new (string.Empty);
        public ElementBinding<string> CurrentAssetsToLoad { get; } = new (string.Empty);
        public ElementBinding<string> CurrentAssetsLoaded { get; } = new (string.Empty);

        public enum Stage : byte
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

        public static readonly Dictionary<Stage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<Stage>())
        {
            [Stage.Init] = 0f,
            [Stage.AuthenticationScreenShown] = 0.05f,
            [Stage.LiveKitConnectionEnsured] = 0.1f,
            [Stage.FeatureFlagInitialized] = 0.15f,
            [Stage.ProfileLoaded] = 0.2f,
            [Stage.EnvironmentMiscSet] = 0.25f,
            [Stage.PlayerAvatarLoaded] = 0.4f,
            [Stage.LandscapeLoaded] = 0.7f,
            [Stage.OnboardingChecked] = 0.80f,
            [Stage.RealmRestarted] = 0.85f,
            [Stage.PlayerTeleported] = 0.95f,
            [Stage.GlobalPXsLoaded] = 0.99f,
            [Stage.Completed] = 1f,
        };


        public float SetCompletedStage(Stage stage)
        {
            CurrentCompletedStageBinding.Value = stage.ToString();
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
