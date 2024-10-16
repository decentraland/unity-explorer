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
        public ElementBinding<string> CurrentStageBinding { get; }= new (string.Empty);
        public ElementBinding<string> CurrentAssetsStateBinding { get; } = new (string.Empty);

        private Action<LoadingStage> analyticsCallback;
        

        private static readonly Dictionary<LoadingStage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<LoadingStage>())
        {
            [LoadingStage.Init] = 0f, 
            [LoadingStage.AuthenticationScreenShowing] = 0.05f, 
            //Used in initialization Flow
            [LoadingStage.LiveKitConnectionEnsuring] = 0.1f,
            //Used in Teleport Flow
            [LoadingStage.LivekitStopping] = 0.1f, 
            [LoadingStage.FeatureFlagInitializing] = 0.15f,
            [LoadingStage.ProfileLoading] = 0.2f, 
            [LoadingStage.EnvironmentMiscSetting] = 0.25f, 
            [LoadingStage.PlayerAvatarLoading] = 0.4f,
            [LoadingStage.LandscapeLoading] = 0.7f,
            [LoadingStage.OnboardingChecking] = 0.80f,
            [LoadingStage.RealmRestarting] = 0.85f, 
            [LoadingStage.PlayerTeleporting] = 0.95f, 
            //Used in initialization Flow
            [LoadingStage.GlobalPXsLoading] = 0.99f,
            //Used in Teleport Flow
            [LoadingStage.LivekitRestarting] = 0.99f, 
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
            LivekitStopping,
            PlayerTeleporting,
            LivekitRestarting,
            GlobalPXsLoading,
            Completed
        }


        public float SetCurrentStage(LoadingStage stage)
        {
            //After the first loading screen flow, we dont want to report analytics anymore
            if (stage == LoadingStage.Completed)
                CurrentStage.Unsubscribe(analyticsCallback);
            
            CurrentStage.Value = stage;
            CurrentStageBinding.Value = stage.ToString();
            return PROGRESS[stage];
        }

        public void ReportAnalytics(Action<LoadingStage> analyticsReport)
        {
            analyticsCallback = analyticsReport;
            CurrentStage.Subscribe(analyticsReport);
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            CurrentAssetsStateBinding.Value = $"{assetsLoaded.ToString()}/{assetsToLoad.ToString()}";
        }

      
    }
}
