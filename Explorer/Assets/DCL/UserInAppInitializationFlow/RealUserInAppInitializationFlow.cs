using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.HealthChecks;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.UserInAppInitializationFlow.StartupOperations.Struct;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.DebugSettings;
using MVC;
using PortableExperiences.Controller;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInAppInitializationFlow : IUserInAppInitializationFlow
    {
        private static readonly ILoadingScreen.EmptyLoadingScreen EMPTY_LOADING_SCREEN = new ();

        private readonly ILoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly AudioClipConfig backgroundMusic;
        private readonly IRealmNavigator realmNavigator;
        private readonly ILoadingScreen loadingScreen;
        private readonly IRoomHub roomHub;
        private readonly LoadPlayerAvatarStartupOperation loadPlayerAvatarStartupOperation;
        private readonly CheckOnboardingStartupOperation checkOnboardingStartupOperation;
        private readonly RestartRealmStartupOperation restartRealmStartupOperation;
        private readonly IStartupOperation startupOperation;

        public RealUserInAppInitializationFlow(
            ILoadingStatus loadingStatus,
            IHealthCheck livekitHealthCheck,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IFeatureFlagsProvider featureFlagsProvider,
            FeatureFlagsCache featureFlagsCache,
            IWeb3IdentityCache web3IdentityCache,
            IRealmController realmController,
            IAppArgs appParameters,
            IDebugSettings debugSettings,
            IPortableExperiencesController portableExperiencesController,
            IRoomHub roomHub
        )
        {
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.roomHub = roomHub;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, livekitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(loadingStatus, featureFlagsProvider, web3IdentityCache, decentralandUrlsSource, appParameters);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var switchRealmMiscVisibilityStartupOperation = new SwitchRealmMiscVisibilityStartupOperation(loadingStatus, realmNavigator);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, mainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, realmNavigator);
            checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, featureFlagsCache, decentralandUrlsSource, appParameters, realmNavigator);
            restartRealmStartupOperation = new RestartRealmStartupOperation(loadingStatus, realmController);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmNavigator, startParcel);
            var loadGlobalPxOperation = new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, selfProfile, featureFlagsCache, debugSettings, portableExperiencesController);

            startupOperation = new SequentialStartupOperation(
                loadingStatus,
                ensureLivekitConnectionStartupOperation,
                initializeFeatureFlagsStartupOperation,
                preloadProfileStartupOperation,
                switchRealmMiscVisibilityStartupOperation,
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                checkOnboardingStartupOperation,
                restartRealmStartupOperation,
                teleportStartupOperation,
                loadGlobalPxOperation
            );
        }

        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            Result result = default;

            loadPlayerAvatarStartupOperation.AssignWorld(parameters.World, parameters.PlayerEntity);
            restartRealmStartupOperation.EnableReload(parameters.ReloadRealm);

            using var playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (parameters.FromLogout)
                    // Disconnect current livekit connection on logout so the avatar is removed from other peers
                    await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));

                if (parameters.ShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);
                    await ShowAuthenticationScreenAsync(ct);
                }

                if (parameters.FromLogout)
                {
                    // If we are coming from a logout, we teleport the user to Genesis Plaza
                    var teleportResult = await realmNavigator.TryInitializeTeleportToParcelAsync(Vector2Int.zero, ct);
                    // Restart livekit connection
                    await roomHub.StartAsync().Timeout(TimeSpan.FromSeconds(10));
                    result = teleportResult.Success ? teleportResult : Result.ErrorResult(teleportResult.ErrorMessage);
                    // We need to flag the process as completed, otherwise the multiplayer systems will not run
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
                }
                else
                {
                    var loadingResult = await LoadingScreen(parameters.ShowLoading)
                       .ShowWhileExecuteTaskAsync(
                            async (parentLoadReport, ct) =>
                            {
                                result = await startupOperation.ExecuteAsync(parentLoadReport, ct);

                                if (result.Success)
                                    parentLoadReport.SetProgress(loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));

                                return result;
                            },
                            ct
                        );

                    ApplyErrorIfLoadingScreenError(ref result, loadingResult);
                }

                if (result.Success == false)
                    ReportHub.LogError(ReportCategory.DEBUG, result.ErrorMessage!);

                //TODO notification popup on failure
            }
            while (result.Success == false && parameters.ShowAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(parameters.World, parameters.PlayerEntity, ct);
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
        }

        private static void ApplyErrorIfLoadingScreenError(ref Result result, Result showResult)
        {
            if (!showResult.Success)
                result = Result.ErrorResult(showResult.ErrorMessage);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
