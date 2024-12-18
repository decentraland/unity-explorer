using System;
using System.Threading;
using System.Threading.Tasks;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Chat.History;
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
        private readonly LoadPlayerAvatarStartupOperation loadPlayerAvatarStartupOperation;
        private readonly CheckOnboardingStartupOperation checkOnboardingStartupOperation;
        private readonly IStartupOperation startupOperation;
        private readonly IStartupOperation reloginOperation;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IChatHistory chatHistory;

        private readonly IRealmController realmController;
        private readonly IRoomHub roomHub;
        private readonly IPortableExperiencesController portableExperiencesController;

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
            ILandscape landscape,
            IAppArgs appParameters,
            IDebugSettings debugSettings,
            IPortableExperiencesController portableExperiencesController,
            DiagnosticsContainer diagnosticsContainer, IChatHistory chatHistory, IRoomHub roomHub)
        {
            this.loadingStatus = loadingStatus;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.portableExperiencesController = portableExperiencesController;
            this.chatHistory = chatHistory;
            this.roomHub = roomHub;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, livekitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(loadingStatus, featureFlagsProvider, web3IdentityCache, decentralandUrlsSource, appParameters);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var switchRealmMiscVisibilityStartupOperation = new SwitchRealmMiscVisibilityStartupOperation(loadingStatus, realmNavigator);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, mainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, landscape);
            checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, featureFlagsCache, decentralandUrlsSource, appParameters, realmNavigator);
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
                teleportStartupOperation,
                loadGlobalPxOperation,
                sentryDiagnostics
            );

            reloginOperation = new SequentialStartupOperation(
                loadingStatus,
                ensureLivekitConnectionStartupOperation,
                preloadProfileStartupOperation,
                switchRealmMiscVisibilityStartupOperation,
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                checkOnboardingStartupOperation,
                teleportStartupOperation,
                loadGlobalPxOperation,
                sentryDiagnostics);
        }


        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            Result result = default;

            loadPlayerAvatarStartupOperation.AssignWorld(parameters.World, parameters.PlayerEntity);

            using UIAudioEventsBus.PlayAudioScope playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (parameters.ShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);
                    if (parameters.FromLogout)
                    {
                        await DoLogoutOperationsAsync();
                        //Restart the realm and show the authentications screen simultaneously to avoid the "empty space" flicker
                        await UniTask.WhenAll(ShowAuthenticationScreenAsync(ct),
                            realmController.SetRealmAsync(
                                URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct));
                    }
                    else
                    {
                        await ShowAuthenticationScreenAsync(ct);
                    }
                }

                var flowToRun = parameters.FromLogout ? reloginOperation : startupOperation;

                var loadingResult = await LoadingScreen(parameters.ShowLoading)
                    .ShowWhileExecuteTaskAsync(
                        async (parentLoadReport, ct) =>
                        {
                            result = await flowToRun.ExecuteAsync(parentLoadReport, ct);

                            if (result.Success)
                                parentLoadReport.SetProgress(
                                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));

                            return result;
                        },
                        ct
                    );

                ApplyErrorIfLoadingScreenError(ref result, loadingResult);

                if (result.Success == false)
                    ReportHub.LogError(ReportCategory.DEBUG, result.ErrorMessage!);

                //TODO notification popup on failure
            }
            while (result.Success == false && parameters.ShowAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(parameters.World, parameters.PlayerEntity, ct);
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
        }

        private async UniTask DoLogoutOperationsAsync()
        {
            portableExperiencesController.UnloadAllPortableExperiences();
            realmNavigator.RemoveCameraSamplingData();
            chatHistory.Clear();
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
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
