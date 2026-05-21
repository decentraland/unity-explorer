using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Character;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Prefs;
using DCL.PrivateWorlds;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UI.ErrorPopup;
using DCL.Utilities;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using MVC;
using PortableExperiences.Controller;
using UnityEngine;
using Utility;
using ChatMessage = DCL.Chat.History.ChatMessage;

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
        private readonly IRoomHub roomHub;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAppArgs appArgs;
        private readonly EnsureLivekitConnectionStartupOperation ensureLivekitConnectionStartupOperation;

        private readonly ICharacterObject characterObject;
        private readonly ExposedTransform characterExposedTransform;
        private readonly StartParcel startParcel;
        private readonly bool isLocalSceneDevelopment;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IChatHistory chatHistory;

        public RealUserInAppInitializationFlow(
            ILoadingStatus loadingStatus,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IRealmController realmController,
            IPortableExperiencesController portableExperiencesController,
            IRoomHub roomHub,
            SequentialLoadingOperation<IStartupOperation.Params> initOps,
            SequentialLoadingOperation<IStartupOperation.Params> reloginOps,
            IWeb3IdentityCache identityCache,
            EnsureLivekitConnectionStartupOperation ensureLivekitConnectionStartupOperation,
            IAppArgs appArgs,
            ICharacterObject characterObject,
            ExposedTransform characterExposedTransform,
            StartParcel startParcel,
            bool isLocalSceneDevelopment,
            IWorldPermissionsService worldPermissionsService,
            IChatHistory chatHistory)
        {
            this.initOps = initOps;
            this.reloginOps = reloginOps;
            this.identityCache = identityCache;
            this.ensureLivekitConnectionStartupOperation = ensureLivekitConnectionStartupOperation;
            this.appArgs = appArgs;
            this.characterObject = characterObject;
            this.startParcel = startParcel;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
            this.characterExposedTransform = characterExposedTransform;
            this.worldPermissionsService = worldPermissionsService;
            this.chatHistory = chatHistory;

            this.loadingStatus = loadingStatus;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.portableExperiencesController = portableExperiencesController;
            this.roomHub = roomHub;
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
                    appArgs.HasFlagWithValueTrue(AppArgsFlags.LOCAL_SCENE) &&
                    FileDCLPlayerPrefs.PrefsInstanceNumber > 0)
                {
                    identityCache.Clear();
                }

                bool shouldShowAuthentication = parameters.ShowAuthentication &&
                                                !appArgs.HasFlagWithValueTrue(AppArgsFlags.SKIP_AUTH_SCREEN) &&
                                                !appArgs.HasFlag(AppArgsFlags.AUTOPILOT);

                // Force show authentication if there's no valid identity in the cache
                if (!shouldShowAuthentication)
                    shouldShowAuthentication = identityCache.Identity == null || identityCache.Identity.IsExpired;

                // Only a human user can authenticate currently.
                if (shouldShowAuthentication && appArgs.HasFlag(AppArgsFlags.AUTOPILOT))
                    Application.Quit(1);

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
                            // After authentication completes, verify the user can actually access the current realm if it's a world.
                            // The realm was set during bootstrap before the user had a chance to switch accounts, so the identity
                            // that's now authenticated may differ from the one assumed at startup.
                            await VerifyWorldAccessAndFallbackIfNeededAsync(ct);

                            //Set initial position and start async livekit connection
                            characterExposedTransform.Position.Value
                                = characterObject.Controller.transform.position
                                    = startParcel.Peek().ParcelToPositionFlat();

                            // This operation is not awaited immediately to save approximately 3-4 seconds during the load process,
                            // as it runs in parallel with other tasks.
                            // However, this approach introduces potential risks.
                            // If any of the LiveKit parameters change after this call (e.g., realm configuration),
                            // the task may become outdated, leading to an inconsistent state.
                            UniTask<EnumResult<TaskError>> livekitHandshake = ensureLivekitConnectionStartupOperation.LaunchLivekitConnectionAsync(ct);

                            //Create a child report to be able to hold the parallel livekit operation
                            AsyncLoadProcessReport sequentialFlowReport = parentLoadReport.CreateChildReport(0.95f);
                            EnumResult<TaskError> operationResult = await flowToRun.ExecuteAsync(parameters.LoadSource.ToString(), 1, new IStartupOperation.Params(sequentialFlowReport, parameters), ct);

                            // HACK: Game is irrecoverably dead. We dont care anything that goes beyond this
                            if (operationResult.Error is { Exception: UserBlockedException })
                                mvcManager.ShowAsync(BlockedScreenController.IssueCommand(new BlockedScreenParameters(((UserBlockedException)operationResult.Error.Value.Exception).BanStatusData.ban)), ct).Forget();
                            else
                            {
                                // Finally, wait for livekit to end handshake that started before.
                                // At this point it is necessary that the task did not become invalid by any modification in the process
                                var livekitOperationResult = await livekitHandshake;

                                if (isLocalSceneDevelopment)
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
        }

        private async UniTask VerifyWorldAccessAndFallbackIfNeededAsync(CancellationToken ct)
        {
            if (isLocalSceneDevelopment) return;
            if (!realmController.RealmData.IsWorld()) return;
            if (realmController.CurrentDomain == null) return;

            if (!TryExtractWorldName(realmController.CurrentDomain.Value, out string worldName))
            {
                ReportHub.LogWarning(ReportCategory.REALM,
                    $"[RealmController] Failed to extract world name from realm '{realmController.CurrentDomain.Value.ToString()}'.");
                await GenesisFallbackAsync();
                return;
            }

            WorldAccessCheckContext context;

            try
            {
                context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM,
                    $"[StartUp] Failed to verify world access for '{worldName}' via world permissions: {e.Message}");
                await GenesisFallbackAsync();
                return;
            }

            switch (context.Result)
            {
                case WorldAccessCheckResult.Allowed:
                    return;
                case WorldAccessCheckResult.CheckFailed:
                case WorldAccessCheckResult.AccessDenied:
                case WorldAccessCheckResult.PasswordRequired:
                    ReportHub.LogWarning(ReportCategory.REALM,
                        $"[StartUp] World '{worldName}' is not authorized for auto-entry, falling back to Genesis.");
                    await GenesisFallbackAsync();
                    return;
                default: throw new ArgumentOutOfRangeException();
            }

            async UniTask GenesisFallbackAsync()
            {
                chatHistory.AddMessage(
                    ChatChannel.NEARBY_CHANNEL_ID,
                    ChatChannel.ChatChannelType.NEARBY,
                    ChatMessage.NewFromSystem($"Could not enter '{worldName}' due to world permissions. You were sent to Genesis Plaza."));

                await realmController.SetRealmAsync(
                    URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct);
            }
        }

        private static bool TryExtractWorldName(URLDomain realm, out string worldName)
        {
            worldName = string.Empty;

            if (!Uri.TryCreate(realm.Value, UriKind.Absolute, out Uri? uri))
                return false;

            string path = uri.AbsolutePath.Trim('/');

            if (string.IsNullOrEmpty(path))
                return false;

            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return false;

            worldName = segments[^1];
            return !string.IsNullOrEmpty(worldName);
        }

        // TODO should be an operation
        private async UniTask DoLogoutOperationsAsync()
        {
            portableExperiencesController.UnloadAllPortableExperiences();
            realmNavigator.RemoveCameraSamplingData();
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
        }

        // TODO should be an operation
        private async UniTask DoRecoveryOperationsAsync()
        {
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
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
                return mvcManager.ShowAsync(BlockedScreenController.IssueCommand(new BlockedScreenParameters(((UserBlockedException)result.Error.Value.Exception).BanStatusData.ban)), ct);

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
