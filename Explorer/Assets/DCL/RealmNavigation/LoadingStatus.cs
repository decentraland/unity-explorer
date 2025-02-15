using DCL.Utilities;
using System.Collections.Generic;
using Utility;

namespace DCL.RealmNavigation
{
    public class LoadingStatus : ILoadingStatus
    {
        public ReactiveProperty<LoadingStage> CurrentStage { get; } = new (LoadingStage.Init);
        public ReactiveProperty<string> AssetState { get; } = new("NA");

        private static readonly Dictionary<LoadingStage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<LoadingStage>())
        {
            [LoadingStage.Init] = 0f,
            [LoadingStage.AuthenticationScreenShowing] = 0.05f,
            [LoadingStage.UnloadCacheChecking] = 0.05f,
            [LoadingStage.LiveKitStopping] = 0.1f, //Used in Teleport Flow
            [LoadingStage.RealmChanging] = 0.25f, //Used in Teleport Flow
            [LoadingStage.ProfileLoading] = 0.1f,
            [LoadingStage.PlayerAvatarLoading] = 0.3f,
            [LoadingStage.LandscapeLoading] = 0.6f,
            [LoadingStage.OnboardingChecking] = 0.7f,
            [LoadingStage.RealmRestarting] = 0.75f, //Used in initialization Flow // TODO not used!
            [LoadingStage.PlayerTeleporting] = 0.85f,
            [LoadingStage.LiveKitConnectionEnsuring] = 0.95f, //Used in initialization Flow
            [LoadingStage.GlobalPXsLoading] = 0.99f, //Used in initialization Flow
            [LoadingStage.LivekitRestarting] = 0.99f, //Used in Teleport Flow
            [LoadingStage.Completed] = 1f
        };


        public enum LoadingStage : byte
        {
            Init,
            AuthenticationScreenShowing,
            LiveKitConnectionEnsuring,
            UnloadCacheChecking,
            ProfileLoading,
            PlayerAvatarLoading,
            LandscapeLoading,
            OnboardingChecking,
            RealmRestarting,
            RealmChanging,

            LiveKitStopping,
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
