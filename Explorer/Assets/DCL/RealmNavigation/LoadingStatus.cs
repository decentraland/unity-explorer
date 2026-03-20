using DCL.Diagnostics;
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
            [LoadingStage.ProfileLoading] = 0.4f,
            [LoadingStage.PlayerAvatarLoading] = 0.5f,
            [LoadingStage.LandscapeLoading] = 0.6f,
            [LoadingStage.PlayerTeleporting] = 0.85f,
            [LoadingStage.GlobalPXsLoading] = 0.99f, //Used in initialization Flow
            [LoadingStage.LivekitRestarting] = 0.99f, //Used in Teleport Flow
            [LoadingStage.Completed] = 1f
        };

        private static readonly HashSet<LoadingStage> NonLoadingScreenStages = new()
        {
            LoadingStage.Completed,
            LoadingStage.Init,
            LoadingStage.AuthenticationScreenShowing,
        };


        public enum LoadingStage : byte
        {
            // Initial loading stages, in order
            Init = 0,
            AuthenticationScreenShowing = 1,
            ProfileLoading = 2,
            PlayerAvatarLoading = 3,
            LandscapeLoading = 4,
            PlayerTeleporting = 5,
            GlobalPXsLoading = 6,
            Completed = 7,

            // Others
            UnloadCacheChecking,
            LiveKitStopping,
            RealmChanging,
            LivekitRestarting,
        }

        public float SetCurrentStage(LoadingStage stage)
        {
            ReportHub.LogProductionInfo($"Current loading stage: {stage}");
            CurrentStage.Value = stage;
            return PROGRESS[stage];
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            AssetState.Value = $"{assetsLoaded.ToString()}/{assetsToLoad.ToString()}";
        }

        public bool IsLoadingScreenOn() =>
            !NonLoadingScreenStages.Contains(CurrentStage.Value);
    }
}
