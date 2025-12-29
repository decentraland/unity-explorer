using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Prefs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings.Utils;
using DCL.UI;
using Global.AppArgs;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Utility;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        public enum AuthenticationStatus
        {
            Init,
            FetchingProfileCached,
            LoggedInCached,

            Login,
            VerificationInProgress,
            FetchingProfile,
            LoggedIn,
        }

        internal const int ANIMATION_DELAY = 300;

        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly SplashScreen splashScreen;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly BuildData buildData;
        private readonly AudioMixerVolumesController audioMixerVolumesController;
        private readonly World world;
        private readonly AuthScreenEmotesSettings emotesSettings;
        private readonly List<Resolution> possibleResolutions = new ();
        private readonly AudioClipConfig backgroundMusic;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly IAppArgs appArgs;

        private const string LOADING_TRANSACTION_NAME = "loading_process";

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        internal UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;
        private readonly IInputBlock inputBlock;
        private float originalWorldAudioVolume;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ReactiveProperty<AuthenticationStatus> CurrentState { get; set; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; private set; } = string.Empty;

        public event Action DiscordButtonClicked;

        private MVCStateMachine<AuthStateBase, AuthStateContext> fsm;
        private AuthenticationScreenAudio audio;

        public AuthenticationScreenController(
            ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            SplashScreen splashScreen,
            CharacterPreviewEventBus characterPreviewEventBus,
            AudioMixerVolumesController audioMixerVolumesController,
            BuildData buildData,
            World world,
            AuthScreenEmotesSettings emotesSettings,
            IInputBlock inputBlock,
            AudioClipConfig backgroundMusic,
            SentryTransactionManager sentryTransactionManager,
            IAppArgs appArgs)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreen = splashScreen;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.buildData = buildData;
            this.world = world;
            this.emotesSettings = emotesSettings;
            this.inputBlock = inputBlock;
            this.backgroundMusic = backgroundMusic;
            this.sentryTransactionManager = sentryTransactionManager;
            this.appArgs = appArgs;

            possibleResolutions.AddRange(ResolutionUtils.GetAvailableResolutions());
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            characterPreviewController?.Dispose();
            web3Authenticator.SetVerificationListener(null);
            audio.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            audio = new AuthenticationScreenAudio(viewInstance, audioMixerVolumesController, backgroundMusic);

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];

            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.ExitButton.onClick.AddListener(ExitApplication);
            viewInstance.MuteButton.Button.onClick.AddListener(audio.OnMuteButtonClicked);
            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);

            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, emotesSettings, characterPreviewFactory, world, characterPreviewEventBus);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(StartLoginFlowUntilEnd);

            fsm = new MVCStateMachine<AuthStateBase, AuthStateContext>(
                context: new AuthStateContext(),
                states: new AuthStateBase[]
                {
                    new InitAuthScreenState(viewInstance, buildData),
                    new AutoLoginAuthState(viewInstance),
                    new LoginStartAuthState(viewInstance, this, CurrentState),
                    new LoadingAuthState(viewInstance, CurrentState),
                    new VerificationAuthState(viewInstance, this, CurrentState),
                    new LobbyAuthState(viewInstance, this, characterPreviewController, inputBlock),
                }
            );

            fsm.Enter<InitAuthScreenState>();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            CheckValidIdentityAndStartInitialFlowAsync().Forget();
            BlockUnwantedInputs();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            audio.OnShow();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            audio.OnHide();

            CancelLoginProcess();
            CancelVerificationCountdown();
            viewInstance!.FinalizeContainer.SetActive(false);
            web3Authenticator.SetVerificationListener(null);
        }

        private async UniTaskVoid CheckValidIdentityAndStartInitialFlowAsync()
        {
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            if (storedIdentity is { IsExpired: false }

                // Force to re-login if the identity will expire in 24hs or less, so we mitigate the chances on
                // getting the identity expired while in-world, provoking signed-fetch requests to fail
                && storedIdentity.Expiration - DateTime.UtcNow > TimeSpan.FromDays(1))
            {
                CancelLoginProcess();
                loginCancellationToken = new CancellationTokenSource();

                try
                {
                    var identityValidationSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "IdentityValidation",
                        SpanOperation = "auth.identity_validation",
                        Depth = 1
                    };
                    sentryTransactionManager.StartSpan(identityValidationSpan);

                    if (IsUserAllowedToAccessToBeta(storedIdentity))
                    {
                        CurrentState.Value = AuthenticationStatus.FetchingProfileCached;

                        var profileFetchSpan = new SpanData
                        {
                            TransactionName = LOADING_TRANSACTION_NAME,
                            SpanName = "FetchProfileCached",
                            SpanOperation = "auth.profile_fetch",
                            Depth = 1
                        };
                        sentryTransactionManager.StartSpan(profileFetchSpan);

                        await FetchProfileAsync(loginCancellationToken.Token);

                        sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                        CurrentState.Value = AuthenticationStatus.LoggedInCached;
                        fsm.Enter<LobbyAuthState>();
                    }
                    else
                    {
                        sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (cached)");
                        fsm.Enter<LoginStartAuthState, PopupType>(PopupType.RESTRICTED_USER, allowReEnterSameState: true);
                    }
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Profile not found during cached authentication", e);
                    fsm.Enter<LoginStartAuthState>(true);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during cached authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState>(true);
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                fsm.Enter<LoginStartAuthState>(true);
            }

            if (splashScreen != null) // Splash screen is destroyed after first login
                splashScreen.Hide();
        }

        private bool IsUserAllowedToAccessToBeta(IWeb3Identity storedIdentity)
        {
            if (Application.isEditor)
                return true;

            FeatureFlagsConfiguration flags = FeatureFlagsConfiguration.Instance;

            if (!flags.IsEnabled(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT)) return true;

            if (!flags.TryGetCsvPayload(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT, out List<List<string>>? allowedUsersCsv))
                return true;

            bool isUserAllowed = allowedUsersCsv![0]
               .Exists(s => new Web3Address(s).Equals(storedIdentity.Address));

            return isUserAllowed;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask?.TrySetCanceled(ct);
            lifeCycleTask = new UniTaskCompletionSource();
            await lifeCycleTask.Task;
        }

        public void StartLoginFlowUntilEnd()
        {
            CancelLoginProcess();

            // Checks the current screen mode because it could have been overridden with Alt+Enter
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();

            loginCancellationToken = new CancellationTokenSource();
            StartLoginFlowUntilEndAsync(loginCancellationToken.Token).Forget();

            return;

            async UniTaskVoid StartLoginFlowUntilEndAsync(CancellationToken ct)
            {
                try
                {
                    CurrentRequestID = string.Empty;

                    var web3AuthSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "Web3Authentication",
                        SpanOperation = "auth.web3_login",
                        Depth = 1
                    };
                    sentryTransactionManager.StartSpan(web3AuthSpan);

                    web3Authenticator.SetVerificationListener(ShowVerification);
                    IWeb3Identity identity = await web3Authenticator.LoginAsync(ct);
                    web3Authenticator.SetVerificationListener(null);

                    var identityValidationSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "IdentityValidation",
                        SpanOperation = "auth.identity_validation",
                        Depth = 1
                    };
                    sentryTransactionManager.StartSpan(identityValidationSpan);

                    if (IsUserAllowedToAccessToBeta(identity))
                    {
                        fsm.Enter<LoadingAuthState>();

                        var profileFetchSpan = new SpanData
                        {
                            TransactionName = LOADING_TRANSACTION_NAME,
                            SpanName = "FetchProfile",
                            SpanOperation = "auth.profile_fetch",
                            Depth = 1
                        };
                        sentryTransactionManager.StartSpan(profileFetchSpan);

                        await FetchProfileAsync(ct);

                        sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                        CurrentState.Value = AuthenticationStatus.LoggedIn;
                        fsm.Enter<LobbyAuthState>();
                    }
                    else
                    {
                        sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (main)");
                        fsm.Enter<LoginStartAuthState, PopupType>(PopupType.RESTRICTED_USER, allowReEnterSameState: true);
                    }
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
                }
                catch (SignatureExpiredException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
                }
                catch (Web3SignatureException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
                }
                catch (CodeVerificationException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User profile not found", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    fsm.Enter<LoginStartAuthState, PopupType>(PopupType.CONNECTION_ERROR, allowReEnterSameState: true);
                }
                finally
                {
                    RestoreResolutionAndScreenMode();
                }
            }
        }

        private void ShowVerification(int code, DateTime expiration, string requestID)
        {
            viewInstance!.VerificationCodeLabel.text = code.ToString();
            CurrentRequestID = requestID;

            var verificationSpan = new SpanData
            {
                TransactionName = LOADING_TRANSACTION_NAME,
                SpanName = "CodeVerification",
                SpanOperation = "auth.code_verification",
                Depth = 1
            };
            sentryTransactionManager.StartSpan(verificationSpan);

            CancelVerificationCountdown();
            verificationCountdownCancellationToken = new CancellationTokenSource();

            viewInstance.StartVerificationCountdownAsync(expiration,
                             verificationCountdownCancellationToken.Token)
                        .Forget();

            fsm.Enter<VerificationAuthState>();
        }

        private async UniTask FetchProfileAsync(CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null)
                throw new ProfileNotFoundException();

            // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
            profile.IsDirty = true;

            // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
            profile.HasConnectedWeb3 = true;

            profileNameLabel!.Value = IsNewUser() ? profile.Name : "back " + profile.Name;
            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);

            return;

            bool IsNewUser() =>
                profile.Version == 1;
        }

        private void ChangeAccount()
        {
            async UniTaskVoid ChangeAccountAsync(CancellationToken ct)
            {
                viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.TO_OTHER);
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                await web3Authenticator.LogoutAsync(ct);

                fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
            }

            characterPreviewController?.OnHide();
            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            ChangeAccountAsync(loginCancellationToken.Token).Forget();
        }

        private void RestoreResolutionAndScreenMode()
        {
            Resolution targetResolution = WindowModeUtils.GetTargetResolution(possibleResolutions);
            FullScreenMode targetScreenMode = WindowModeUtils.GetTargetScreenMode(appArgs.HasFlag(AppArgsFlags.WINDOWED_MODE));
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }

        public void CancelLoginProcess()
        {
            loginCancellationToken?.SafeCancelAndDispose();
            loginCancellationToken = null;
        }

        private void OpenDiscord()
        {
            webBrowser.OpenUrl(DecentralandUrl.DiscordLink);
            DiscordButtonClicked?.Invoke();
        }

        private void ExitApplication()
        {
            CancelLoginProcess();
            ExitUtils.Exit();
        }

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private void RequestAlphaAccess() =>
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);

        private void CloseErrorPopup() =>
            viewInstance!.ErrorPopupRoot.SetActive(false);

        private void BlockUnwantedInputs() =>
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
    }
}
