using System;
using DCL.Utilities;
using System.Collections.Generic;
using DCL.DebugUtilities.UIBindings;
using Utility;

namespace DCL.UserInAppInitializationFlow
{
    public class LoadingStatus : ILoadingStatus
    {
        public ReactiveProperty<LoadingStage> CurrentStage { get; } = new (LoadingStage.Init);
        public ReactiveProperty<string> AssetState { get; } = new("NA");

        private static readonly Dictionary<LoadingStage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<LoadingStage>())
        {
            [LoadingStage.Init] = 0f, 
            [LoadingStage.AuthenticationScreenShowing] = 0.05f, 
            [LoadingStage.LiveKitConnectionEnsuring] = 0.1f, //Used in initialization Flow
            [LoadingStage.LivekitStopping] = 0.1f, //Used in Teleport Flow
            [LoadingStage.FeatureFlagInitializing] = 0.15f,
            [LoadingStage.RealmChanging] = 0.25f, //Used in Teleport Flow
            [LoadingStage.ProfileLoading] = 0.2f, 
            [LoadingStage.EnvironmentMiscSetting] = 0.25f, 
            [LoadingStage.PlayerAvatarLoading] = 0.4f,
            [LoadingStage.LandscapeLoading] = 0.7f,
            [LoadingStage.OnboardingChecking] = 0.80f,
            [LoadingStage.RealmRestarting] = 0.85f, //Used in initialization Flow
            [LoadingStage.PlayerTeleporting] = 0.95f, 
            [LoadingStage.GlobalPXsLoading] = 0.99f, //Used in initialization Flow
            [LoadingStage.LivekitRestarting] = 0.99f, //Used in Teleport Flow
            [LoadingStage.Completed] = 1f
        };


        public enum LoadingStage : byte
        {
            Init,
            AuthenticationScreenShowing,
            LiveKitConnectionEnsuring,
            FeatureFlagInitializing,
            ProfileLoading,
            EnvironmentMiscSetting,
            PlayerAvatarLoading,
            LandscapeLoading,
            OnboardingChecking,
            RealmRestarting,
            RealmChanging,

            LivekitStopping,
            PlayerTeleporting,
            LivekitRestarting,
            GlobalPXsLoading,
            Completed
        }


        public float SetCurrentStage(LoadingStage stage)
        {
            CurrentStage.Value = stage;
            return PROGRESS[stage];
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            AssetState.Value = $"{assetsLoaded.ToString()}/{assetsToLoad.ToString()}";
        }

      
    }
}
