using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Character;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Lobby;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Pulse;
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
        private readonly SequentialLoadingOperation<IStartupOperation.Params> initOps;
        private readonly SequentialLoadingOperation<IStartupOperation.Params> reloginOps;

        private readonly IRealmController realmController;
        private readonly IRoomHub roomHub;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAppArgs appArgs;
        private readonly IPulseMultiplayerService pulseMultiplayerService;
        private readonly EnsureLivekitConnectionStartupOperation ensureLivekitConnectionStartupOperation;

        private readonly ICharacterObject characterObject;
        private readonly ExposedTransform characterExposedTransform;
        private readonly StartParcel startParcel;
        private readonly bool isLocalSceneDevelopment;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IChatHistory chatHistory;
        private readonly URLDomain genesisDomain;

        // Cancelled by a Logout execution so the execution parked on the startup lobby gives the flow up instead of loading the world
        private CancellationTokenSource? startupLobbyGate;

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
            IPulseMultiplayerService pulseMultiplayerService,
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
            this.pulseMultiplayerService = pulseMultiplayerService;
            this.characterExposedTransform = characterExposedTransform;
            this.worldPermissionsService = worldPermissionsService;
            this.chatHistory = chatHistory;

            this.loadingStatus = loadingStatus;
            genesisDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));
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
                                                !appArgs.HasFlag(AppArgsFlags.AUTOPILOT) &&
                                                !appArgs.HasFlag(AppArgsFlags.MEASURE_LOADING_TIME);

                // Force show authentication if there's no valid identity in the cache
                if (!shouldShowAuthentication)
                    shouldShowAuthentication = identityCache.Identity == null || identityCache.Identity.IsExpired;

                // Only a human user can authenticate currently.
                if (shouldShowAuthentication && (appArgs.HasFlag(AppArgsFlags.AUTOPILOT) || appArgs.HasFlag(AppArgsFlags.MEASURE_LOADING_TIME)))
                    Application.Quit(1);

                if (shouldShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);

                    switch (parameters.LoadSource)
                    {
                        case IUserInAppInitializationFlow.LoadSource.Logout:
                            startupLobbyGate?.Cancel();

                            // The start parcel consumed by the session that ends here goes back to the launch destination for the one that starts
                            startParcel.Reset();
                            await DoLogoutOperationsAsync();

                            //Restart the realm and show the authentications screen simultaneously to avoid the "empty space" flicker
                            //No error should be possible at this point
                            // TODO move SetRealmAsync to an operation
                            await UniTask.WhenAll(ShowAuthenticationScreenAsync(parameters, ct),
                                realmController.SetRealmAsync(genesisDomain, ct));

                            break;
                        case IUserInAppInitializationFlow.LoadSource.Recover:
                            await DoRecoveryOperationsAsync();
                            goto default;
                        default:
                            await UniTask.WhenAll(
                                ShowAuthenticationScreenAsync(parameters, ct),
                                ShowErrorPopupIfRequired(result, ct)
                            );

                            break;
                    }
                }

                // Nothing has been teleported or loaded yet: the lobby holds the flow until the user jumps in
                if (ShouldShowStartupLobby(FeaturesRegistry.Instance.IsEnabled(FeatureId.Lobby), appArgs, startParcel, parameters.LoadSource) && !await WaitForLobbyJumpInAsync(ct))
                    return;

                var flowToRun = parameters.LoadSource is IUserInAppInitializationFlow.LoadSource.Logout
                    ? reloginOps
                    : initOps;

                var loadingResult = await LoadingScreen(parameters.ShowLoading)
                    .ShowWhileExecuteTaskAsync(
                        async (parentLoadReport, loadCt) =>
                        {
                            await ApplyStartRealmAsync(startParcel, realmController, chatHistory, genesisDomain, loadCt);

                            // After authentication completes, verify the user can actually access the current realm if it's a world.
                            // The realm was set during bootstrap before the user had a chance to switch accounts, so the identity
                            // that's now authenticated may differ from the one assumed at startup.
                            await VerifyWorldAccessAndFallbackIfNeededAsync(loadCt);

                            //Set initial position and start async livekit connection
                            characterExposedTransform.Position.Value
                                = characterObject.Controller.transform.position
                                    = startParcel.Peek().ParcelToPositionFlat();

                            // This operation is not awaited immediately to save approximately 3-4 seconds during the load process,
                            // as it runs in parallel with other tasks.
                            // However, this approach introduces potential risks.
                            // If any of the LiveKit parameters change after this call (e.g., realm configuration),
                            // the task may become outdated, leading to an inconsistent state.
                            UniTask<EnumResult<TaskError>> livekitHandshake = ensureLivekitConnectionStartupOperation.LaunchLivekitConnectionAsync(loadCt);

                            //Create a child report to be able to hold the parallel livekit operation
                            AsyncLoadProcessReport sequentialFlowReport = parentLoadReport.CreateChildReport(0.95f);
                            EnumResult<TaskError> operationResult = await flowToRun.ExecuteAsync(parameters.LoadSource.ToString(), 1, new IStartupOperation.Params(sequentialFlowReport, parameters), loadCt);

                            // HACK: Game is irrecoverably dead. We dont care anything that goes beyond this
                            if (operationResult.Error is { Exception: UserBlockedException })
                                mvcManager.ShowAsync(BlockedScreenController.IssueCommand(new BlockedScreenParameters(((UserBlockedException)operationResult.Error.Value.Exception).BanStatusData.ban)), loadCt).Forget();
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

                                    if (operationResult.Success)
                                        startParcel.MarkLanded();
                                }
                                else
                                {
                                    // A failed own-profile step keeps its own error; any other outcome is decided by the LiveKit result
                                    if (!IsOwnProfileFailure(operationResult))
                                        operationResult = livekitOperationResult;

                                    if (operationResult.Success)
                                    {
                                        parentLoadReport.SetProgress(
                                            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));

                                        startParcel.MarkLanded();
                                    }
                                }

                                // TODO: redesign. Clearing the identity cache as a side effect of an error, only to steer the auth decision at the top of the loop, is implicit control flow;
                                // the retry should decide whether to show the auth screen from the result itself
                                if (RequiresReauthentication(operationResult))
                                    identityCache.Clear();
                            }

                            return operationResult;
                        },
                        ct
                    );

                result = loadingResult;

                if (!result.Success)
                {
                    //Fail straight away
                    string message = result.Error.AsMessage();
                    ReportHub.LogError(ReportCategory.AUTHENTICATION, message);

                    // A link on the retried auth screen may still pick another realm before the next switch
                    startParcel.ClearRealmApplied();
                }
            }
            while (!result.Success && parameters.ShowAuthentication);
        }

        internal static bool ShouldShowStartupLobby(bool lobbyEnabled, IAppArgs appArgs, StartParcel startParcel, IUserInAppInitializationFlow.LoadSource loadSource) =>
            lobbyEnabled
            && loadSource != IUserInAppInitializationFlow.LoadSource.Recover
            && !LandsAtLaunchDestination(appArgs, loadSource)
            && !startParcel.JumpInRequested
            && !appArgs.HasFlagWithValueTrue(AppArgsFlags.SKIP_AUTH_SCREEN)
            && !appArgs.HasFlag(AppArgsFlags.AUTOPILOT)
            && !appArgs.HasFlag(AppArgsFlags.MEASURE_LOADING_TIME)
            && !appArgs.HasFlag(AppArgsFlags.DISABLE_HUD);

        /// <summary>A missing profile is resolved only by signing in again, so the cached identity cannot be retried as is.</summary>
        internal static bool RequiresReauthentication(EnumResult<TaskError> result) =>
            result.Error is { Exception: ProfileNotFoundException };

        /// <summary>The sequence stops at the own-profile step when it fails, so nothing after it ran and the player entity has no profile.</summary>
        internal static bool IsOwnProfileFailure(EnumResult<TaskError> result) =>
            result.Error is { Exception: ProfileNotFoundException or ProfileFetchFailedException };

        internal static bool LandsAtLaunchDestination(IAppArgs appArgs, IUserInAppInitializationFlow.LoadSource loadSource) =>
            loadSource == IUserInAppInitializationFlow.LoadSource.StartUp && appArgs.HasLaunchDestination();

        /// <summary>
        ///     Switches to the realm picked before anything is loaded. A Genesis pick is satisfied by any Genesis realm, and an unreachable realm keeps the current one.
        /// </summary>
        internal static async UniTask ApplyStartRealmAsync(StartParcel startParcel, IRealmController realmController, IChatHistory chatHistory, URLDomain genesis, CancellationToken ct)
        {
            startParcel.MarkRealmApplied();

            if (startParcel.Realm is not { } realm) return;
            if (realm == realmController.CurrentDomain) return;
            if (realmController.RealmData.IsGenesis() && realm == genesis) return;

            if (!await realmController.IsReachableAsync(realm, ct))
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"Startup realm {realm} is not reachable, keeping {realmController.CurrentDomain}");
                string destination = TryExtractWorldName(realm, out string worldName) ? worldName : realm.Value;
                chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, ChatMessage.NewFromSystem($"Could not reach '{destination}'. You were sent to {realmController.RealmData.RealmName}."));

                // The parcel that came with the unreachable realm belongs to it
                if (realmController.CurrentDomain is { } kept)
                    startParcel.AssignRealm(kept);

                return;
            }

            await realmController.SetRealmAsync(realm, ct);
        }

        /// <summary>
        ///     Leaves a world the player cannot enter for Genesis and tells the player so.
        /// </summary>
        internal static async UniTask FallBackToGenesisAsync(StartParcel startParcel, IRealmController realmController, IChatHistory chatHistory, URLDomain genesis, string worldName, CancellationToken ct)
        {
            chatHistory.AddMessage(
                ChatChannel.NEARBY_CHANNEL_ID,
                ChatChannel.ChatChannelType.NEARBY,
                ChatMessage.NewFromSystem($"Could not enter '{worldName}' due to world permissions. You were sent to Genesis Plaza."));

            // The parcel that came with the denied world belongs to it
            startParcel.AssignRealm(genesis);
            await realmController.SetRealmAsync(genesis, ct);
        }

        /// <summary>
        ///     Holds the flow until the user picks a destination; false when a Logout took the flow over, so this execution must not load the world.
        /// </summary>
        private async UniTask<bool> WaitForLobbyJumpInAsync(CancellationToken ct)
        {
            startupLobbyGate = startupLobbyGate.SafeRestart();
            CancellationToken gateToken = startupLobbyGate.Token;

            var jumpIn = new UniTaskCompletionSource();

            // The lobby steps aside for the panels it opens and comes back, so the flow waits for the pick rather than for the lobby leaving the screen
            mvcManager.ShowAndForget(LobbyDocumentController.IssueCommand(new LobbyParameter(isStartup: true, () => jumpIn.TrySetResult(), gateToken)), ct);

            using (CancellationTokenSource lobbyUp = CancellationTokenSource.CreateLinkedTokenSource(ct, gateToken))
                await jumpIn.Task.AttachExternalCancellation(lobbyUp.Token).SuppressCancellationThrow();

            ct.ThrowIfCancellationRequested();

            bool jumpedIn = !gateToken.IsCancellationRequested;

            // A Logout execution may have opened its own lobby meanwhile; only the gate created here is released
            if (startupLobbyGate != null && startupLobbyGate.Token == gateToken)
            {
                startupLobbyGate.Dispose();
                startupLobbyGate = null;
            }

            return jumpedIn;
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
                await FallBackToGenesisAsync(startParcel, realmController, chatHistory, genesisDomain, worldName, ct);
                return;
            }

            WorldAccessCheckContext context;

            try
            {
                context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM,
                    $"[StartUp] Failed to verify world access for '{worldName}' via world permissions: {e.Message}");
                await FallBackToGenesisAsync(startParcel, realmController, chatHistory, genesisDomain, worldName, ct);
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
                    await FallBackToGenesisAsync(startParcel, realmController, chatHistory, genesisDomain, worldName, ct);
                    return;
                default: throw new ArgumentOutOfRangeException();
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
            await pulseMultiplayerService.DisconnectAsync();
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
        }

        // TODO should be an operation
        private async UniTask DoRecoveryOperationsAsync()
        {
            await pulseMultiplayerService.DisconnectAsync();
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
        }

        private async UniTask ShowAuthenticationScreenAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            var authParams = new AuthenticationScreenController.Params(parameters.StartAtLoginSelection, LandsAtLaunchDestination(appArgs, parameters.LoadSource));
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(authParams), ct);
        }

        private UniTask ShowErrorPopupIfRequired(EnumResult<TaskError> result, CancellationToken ct)
        {
            if (result.Success)
                return UniTask.CompletedTask;

            if (result.Error is { Exception: UserBlockedException })
                return mvcManager.ShowAsync(BlockedScreenController.IssueCommand(new BlockedScreenParameters(((UserBlockedException)result.Error.Value.Exception).BanStatusData.ban)), ct);

            if (result.Error is { Exception: ProfileNotFoundException })
                return mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(new ErrorPopupWithRetryController.Input(
                    title: "Profile Not Found",
                    description: "We could not find a profile for this account. Did you create your profile? Sign in and complete the account setup, or check that you are using the right wallet.",
                    iconType: ErrorPopupWithRetryController.IconType.Warning,
                    retryText: "Continue")), ct);

            if (result.Error is { State: TaskError.Timeout })
                return mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(new ErrorPopupWithRetryController.Input(
                    title: "Connection Error",
                    description: "We were unable to connect to Decentraland. Please verify your connection and retry.",
                    iconType: ErrorPopupWithRetryController.IconType.ConnectionLost,
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

        private ILoadingScreen LoadingScreen(bool withUi) =>
            withUi ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
