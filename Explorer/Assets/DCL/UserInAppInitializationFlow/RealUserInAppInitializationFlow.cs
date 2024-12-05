using CommunicationData.URLHelpers;
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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
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
            IRealmMisc realmMisc,
            ILandscape landscape,
            IAppArgs appParameters,
            IDebugSettings debugSettings,
            IPortableExperiencesController portableExperiencesController,
            IRoomHub roomHub,
            DiagnosticsContainer diagnosticsContainer)
        {
            this.loadingStatus = loadingStatus;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.roomHub = roomHub;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, livekitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(loadingStatus, featureFlagsProvider, web3IdentityCache, decentralandUrlsSource, appParameters);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var switchRealmMiscVisibilityStartupOperation = new SwitchRealmMiscVisibilityStartupOperation(loadingStatus, realmController, realmMisc);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, mainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, landscape);
            checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, featureFlagsCache, decentralandUrlsSource, appParameters, realmNavigator);
            restartRealmStartupOperation = new RestartRealmStartupOperation(loadingStatus, realmController);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmNavigator, startParcel);
            var loadGlobalPxOperation = new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, selfProfile, featureFlagsCache, debugSettings, portableExperiencesController);
            var sentryDiagnostics = new SentryDiagnosticStartupOperation(realmController, diagnosticsContainer);

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
                loadGlobalPxOperation,
                sentryDiagnostics
            );
        }

        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            Result result = default;

            loadPlayerAvatarStartupOperation.AssignWorld(parameters.World, parameters.PlayerEntity);
            restartRealmStartupOperation.EnableReload(parameters.ReloadRealm);

            using UIAudioEventsBus.PlayAudioScope playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

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
                    // If we are coming from a logout, we teleport the user to Genesis Plaza and force realm change to reset the scene properly
                    var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                    var changeRealmResult = await realmNavigator.TryChangeRealmAsync(url, ct);

                    if (changeRealmResult.Success == false)
                        ReportHub.LogError(ReportCategory.AUTHENTICATION, changeRealmResult.AsResult().ErrorMessage!);

                    // Restart livekit connection
                    await roomHub.StartAsync().Timeout(TimeSpan.FromSeconds(10));
                    result = changeRealmResult.AsResult();

                    // We need to flag the process as completed, otherwise the multiplayer systems will not run
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
                }
                else
                {
                    Result loadingResult = await LoadingScreen(parameters.ShowLoading)
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
                result = showResult;
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
