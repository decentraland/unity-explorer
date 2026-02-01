using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Character;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
#endif

using DCL.Prefs;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UI.ErrorPopup;
using DCL.Utilities;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using MVC;
using PortableExperiences.Controller;
using Utility;

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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly SequentialLoadingOperation<IStartupOperation.Params> initOps;
        private readonly SequentialLoadingOperation<IStartupOperation.Params> reloginOps;

        private readonly IRealmController realmController;

#if !NO_LIVEKIT_MODE
        private readonly IRoomHub roomHub;
#endif

        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly CheckOnboardingStartupOperation checkOnboardingStartupOperation;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAppArgs appArgs;

#if !NO_LIVEKIT_MODE
        private readonly EnsureLivekitConnectionStartupOperation ensureLivekitConnectionStartupOperation;
#endif

        private readonly ICharacterObject characterObject;
        private readonly ExposedTransform characterExposedTransform;
        private readonly StartParcel startParcel;

#if !UNITY_WEBGL
        private readonly bool isLocalSceneDevelopment;
#endif

        public RealUserInAppInitializationFlow(
            ILoadingStatus loadingStatus,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IRealmController realmController,
            IPortableExperiencesController portableExperiencesController,
#if !NO_LIVEKIT_MODE
            IRoomHub roomHub,
#endif
            SequentialLoadingOperation<IStartupOperation.Params> initOps,
            SequentialLoadingOperation<IStartupOperation.Params> reloginOps,
            CheckOnboardingStartupOperation checkOnboardingStartupOperation,
            IWeb3IdentityCache identityCache,
#if !NO_LIVEKIT_MODE
            EnsureLivekitConnectionStartupOperation ensureLivekitConnectionStartupOperation,
#endif
            IAppArgs appArgs,
            ICharacterObject characterObject,
            ExposedTransform characterExposedTransform,
            StartParcel startParcel
#if !UNITY_WEBGL
            , bool isLocalSceneDevelopment
#endif
            )
        {
            this.initOps = initOps;
            this.reloginOps = reloginOps;
            this.checkOnboardingStartupOperation = checkOnboardingStartupOperation;
            this.identityCache = identityCache;
#if !NO_LIVEKIT_MODE
            this.ensureLivekitConnectionStartupOperation = ensureLivekitConnectionStartupOperation;
#endif
            this.appArgs = appArgs;
            this.characterObject = characterObject;
            this.startParcel = startParcel;

#if !UNITY_WEBGL
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
#endif
            this.characterExposedTransform = characterExposedTransform;

            this.loadingStatus = loadingStatus;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.portableExperiencesController = portableExperiencesController;
#if !NO_LIVEKIT_MODE
            this.roomHub = roomHub;
#endif
        }

        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            EnumResult<TaskError> result = parameters.RecoveryError;

            using UIAudioEventsBus.PlayAudioScope playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                // Clear cached identity for non-first instances in local scene development
                // This ensures each instance (except the first one) shows the authentication screen
                if (!appArgs.HasFlagWithValueTrue(AppArgsFlags.SKIP_AUTH_SCREEN) &&
                    appArgs.HasFlagWithValueTrue(AppArgsFlags.LOCAL_SCENE)

#if !UNITY_WEBGL
                    && FileDCLPlayerPrefs.PrefsInstanceNumber > 0
#endif
                    )
                {
                    identityCache.Clear();
                }

                bool shouldShowAuthentication = parameters.ShowAuthentication &&
                                                !appArgs.HasFlagWithValueTrue(AppArgsFlags.SKIP_AUTH_SCREEN);

                // Force show authentication if there's no valid identity in the cache
                if (!shouldShowAuthentication)
                    shouldShowAuthentication = identityCache.Identity == null || identityCache.Identity.IsExpired;

                if (shouldShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);

                    switch (parameters.LoadSource)
                    {
                        case IUserInAppInitializationFlow.LoadSource.Logout:
                            await DoLogoutOperationsAsync();

                            //Restart the realm and show the authentications screen simultaneously to avoid the "empty space" flicker
                            //No error should be possible at this point
                            // TODO move SetRealmAsync to an operation
                            await UniTask.WhenAll(ShowAuthenticationScreenAsync(ct),
                                realmController.SetRealmAsync(
                                    URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct));

                            break;
                        case IUserInAppInitializationFlow.LoadSource.Recover:
                            await DoRecoveryOperationsAsync();
                            goto default;
                        default:
                            await UniTask.WhenAll(
                                ShowAuthenticationScreenAsync(ct),
                                ShowErrorPopupIfRequired(result, ct)
                            );

                            break;
                    }
                }

                var flowToRun = parameters.LoadSource is IUserInAppInitializationFlow.LoadSource.Logout
                    ? reloginOps
                    : initOps;

                var loadingResult = await LoadingScreen(parameters.ShowLoading)
                    .ShowWhileExecuteTaskAsync(
                        async (parentLoadReport, ct) =>
                        {
                            // We need to do this before livekit because there is a realm change in this operation
                            // and we need to ensure that livekit connects to the correct endpoint
                            await checkOnboardingStartupOperation.ExecuteAsync(ct);

                            //Set initial position and start async livekit connection
                            characterExposedTransform.Position.Value
                                = characterObject.Controller.transform.position
                                    = startParcel.Peek().ParcelToPositionFlat();

#if !NO_LIVEKIT_MODE
                            // This operation is not awaited immediately to save approximately 3-4 seconds during the load process,
                            // as it runs in parallel with other tasks.
                            // However, this approach introduces potential risks.
                            // If any of the LiveKit parameters change after this call (e.g., realm configuration),
                            // the task may become outdated, leading to an inconsistent state.
                            UniTask<EnumResult<TaskError>> livekitHandshake = ensureLivekitConnectionStartupOperation.LaunchLivekitConnectionAsync(ct);
#endif

                            //Create a child report to be able to hold the parallel livekit operation
                            AsyncLoadProcessReport sequentialFlowReport = parentLoadReport.CreateChildReport(0.95f);
                            EnumResult<TaskError> operationResult = await flowToRun.ExecuteAsync(parameters.LoadSource.ToString(), 1, new IStartupOperation.Params(sequentialFlowReport, parameters), ct);

                            // HACK: Game is irrecoverably dead. We dont care anything that goes beyond this
                            if (operationResult.Error is { Exception: UserBlockedException })
                                mvcManager.ShowAsync(BlockedScreenController.IssueCommand(), ct);
                            else
                            {
#if !NO_LIVEKIT_MODE
                                // Finally, wait for livekit to end handshake that started before.
                                // At this point it is necessary that the task did not become invalid by any modification in the process
                                var livekitOperationResult = await livekitHandshake;
#endif

#if !UNITY_WEBGL
                                if (FeaturesRegistry.Instance.IsEnabled(FeatureId.LOCAL_SCENE_DEVELOPMENT))
                                {
                                    // Fix: https://github.com/decentraland/unity-explorer/issues/5250
                                    // Prevent creators to be stuck at loading screen due to livekit issues
                                    // Local scene development doesn't strictly need livekit to run
                                    parentLoadReport.SetProgress(
                                        loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
                                }
                                else
                                {
                                    operationResult = livekitOperationResult;

                                    if (operationResult.Success)
                                        parentLoadReport.SetProgress(
                                            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
                                }
#else
                                // Pass in WebGL immediately because there is no livekitOperationResult
                                parentLoadReport.SetProgress(
                                        loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
#endif


                            }

                            return operationResult;
                        },
                        ct
                    );

                result = loadingResult;

                if (result.Success == false)
                {
                    //Fail straight away
                    string message = result.Error.AsMessage();
                    ReportHub.LogError(ReportCategory.AUTHENTICATION, message);
                }
            }
            while (result.Success == false && parameters.ShowAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(parameters.World, parameters.PlayerEntity, ct);
        }

        // TODO should be an operation
        private async UniTask DoLogoutOperationsAsync()
        {
            portableExperiencesController.UnloadAllPortableExperiences();
            realmNavigator.RemoveCameraSamplingData();
#if !NO_LIVEKIT_MODE
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
#endif
        }

        // TODO should be an operation
        private async UniTask DoRecoveryOperationsAsync()
        {
#if !NO_LIVEKIT_MODE
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
#endif
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private UniTask ShowErrorPopupIfRequired(EnumResult<TaskError> result, CancellationToken ct)
        {
            if (result.Success)
                return UniTask.CompletedTask;

            if (result.Error is { Exception: UserBlockedException })
                return mvcManager.ShowAsync(BlockedScreenController.IssueCommand(), ct);

            if (result.Error is { State: TaskError.Timeout })
                return mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(new ErrorPopupWithRetryController.Input(
                    title: "Connection Error",
                    description: "We were unable to connect to Decentraland. Please verify your connection and retry.",
                    iconType: ErrorPopupWithRetryController.IconType.CONNECTION_LOST,
                    retryText: "Continue")), ct);

            var message = $"{ToMessage(result)}\nPlease try again";
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
                       TaskError.Timeout => "Load timeout. Verify yor connection.",
                       TaskError.Cancelled => "Operation cancelled.",
                       TaskError.UnexpectedException => "Critical error occured.",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
