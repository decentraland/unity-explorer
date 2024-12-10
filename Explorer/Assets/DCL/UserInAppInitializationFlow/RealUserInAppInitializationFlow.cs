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
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UI.ErrorPopup;
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
            IAnalyticsController analyticsController,
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
            ).WithAnalytics(analyticsController);
        }

        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            EnumResult<TaskError> result = parameters.RecoveryError;

            loadPlayerAvatarStartupOperation.AssignWorld(parameters.World, parameters.PlayerEntity);
            restartRealmStartupOperation.EnableReload(parameters.ReloadRealm);

            using UIAudioEventsBus.PlayAudioScope playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (parameters.LoadSource is not IUserInAppInitializationFlow.LoadSource.StartUp)

                    // Disconnect current livekit connection on logout so the avatar is removed from other peers
                    await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));

                if (parameters.ShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);

                    await UniTask.WhenAll(
                        ShowAuthenticationScreenAsync(ct),
                        ShowErrorPopupIfRequired(result, ct)
                    );
                }

                if (parameters.LoadSource is IUserInAppInitializationFlow.LoadSource.Logout)
                {
                    // If we are coming from a logout, we teleport the user to Genesis Plaza and force realm change to reset the scene properly
                    var url = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
                    var changeRealmResult = await realmNavigator.TryChangeRealmAsync(url, ct);

                    if (changeRealmResult.Success == false)
                        ReportHub.LogError(ReportCategory.AUTHENTICATION, changeRealmResult.AsResult().ErrorMessage!);

                    // Restart livekit connection
                    await roomHub.StartAsync().Timeout(TimeSpan.FromSeconds(10));

                    result = changeRealmResult.As(ChangeRealmErrors.AsTaskError);

                    // We need to flag the process as completed, otherwise the multiplayer systems will not run
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
                }
                else
                {
                    EnumResult<TaskError> loadingResult = await LoadingScreen(parameters.ShowLoading)
                       .ShowWhileExecuteTaskAsync(
                            async (parentLoadReport, ct) =>
                            {
                                var operationResult = await startupOperation.ExecuteAsync(parentLoadReport, ct);
                                if (operationResult.Success) parentLoadReport.SetProgress(loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
                                return operationResult;
                            },
                            ct
                        );

                    result = loadingResult;
                }

                if (result.Success == false)
                {
                    string message = result.Error.AsMessage();
                    ReportHub.LogError(ReportCategory.AUTHENTICATION, message);
                }
            }
            while (result.Success == false && parameters.ShowAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(parameters.World, parameters.PlayerEntity, ct);
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private UniTask ShowErrorPopupIfRequired(EnumResult<TaskError> result, CancellationToken ct)
        {
            if (result.Success)
                return UniTask.CompletedTask;

            string message = ToMessage(result);
            return mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(ErrorPopupData.FromDescription(message)), ct);
        }

        private string ToMessage(EnumResult<TaskError> result)
        {
            if (result.Success)
            {
                ReportHub.LogError(ReportCategory.AUTHENTICATION, "Incorrect use case of error to message");
                return "Incorrect error state";
            }

            var error = result.Error!.Value;

            return error.State switch
                   {
                       TaskError.MessageError => $"Error: {error.Message}",
                       TaskError.Timeout => "Load timeout",
                       TaskError.Cancelled => "Operation cancelled",
                       TaskError.UnexpectedException => "Critical error occured",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
