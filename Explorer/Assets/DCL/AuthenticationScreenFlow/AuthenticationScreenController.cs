using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Audio;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings.Utils;
using DCL.UI;
using DCL.WebRequests;
using Global.AppArgs;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.Prefs;
using DCL.Utility;
using Sentry;
using ThirdWebUnity;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;
using Avatar = DCL.Profiles.Avatar;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

        private enum ViewState
        {
            Login,
            LoginInProgress,
            Loading,
            Finalize,
            FinalizeNewUser, VerificationOTP,
        }

        private const int ANIMATION_DELAY = 300;

        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
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
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWebRequestController webRequestController;

        // Base wearables randomization - Dictionary<category, List<URN>>
        private Dictionary<string, List<URN>>? maleWearablesByCategory;
        private Dictionary<string, List<URN>>? femaleWearablesByCategory;
        private bool baseWearablesLoaded;

        private const string LOADING_TRANSACTION_NAME = "loading_process";

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private UniTaskCompletionSource<string>? otpCompletionSource;
        private StringVariable? profileNameLabel;
        private IInputBlock inputBlock;
        private float originalWorldAudioVolume;
        private string? currentEmail;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ReactiveProperty<AuthenticationStatus> CurrentState { get; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; private set; } = string.Empty;

        public event Action DiscordButtonClicked;

        public AuthenticationScreenController(
            ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            ICompositeWeb3Provider compositeWeb3Provider,
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
            IAppArgs appArgs,
            IWearablesProvider wearablesProvider,
            IWebRequestController webRequestController)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.compositeWeb3Provider = compositeWeb3Provider;
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
            this.wearablesProvider = wearablesProvider;
            this.webRequestController = webRequestController;

            possibleResolutions.AddRange(ResolutionUtils.GetAvailableResolutions());
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            characterPreviewController?.Dispose();
            web3Authenticator.SetVerificationListener(null);
            web3Authenticator.SetOtpRequestListener(null);
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartDappLoginFlowUntilEnd);

            viewInstance.CancelLoginButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);
            viewInstance.BackButton.onClick.AddListener(CancelLoginAndRestartFromBeginning);

            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.CancelAuthenticationProcessOTP.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);

            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.ExitButton.onClick.AddListener(ExitApplication);
            viewInstance.MuteButton.Button.onClick.AddListener(OnMuteButtonClicked);
            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);

            viewInstance.VersionText.text = Application.isEditor
                ? $"editor-version - {buildData.InstallSource}"
                : $"{Application.version} - {buildData.InstallSource}";

            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, emotesSettings, characterPreviewFactory, world, characterPreviewEventBus);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitUtils.Exit);
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(StartDappLoginFlowUntilEnd);

            // ThirdWeb buttons
            viewInstance.LoginWithOtpButton.onClick.AddListener(StartOTPLoginFlowUntilEnd);
            viewInstance.OTPInputField.OnCodeComplete += SendRegistration;
            viewInstance.FinalizeNewUserButton.onClick.AddListener(FinalizeNewUser);

            viewInstance.RandomizeButton.onClick.AddListener(RandomizeAvatar);
            viewInstance.PrevRandomButton.onClick.AddListener(PrevRandomAvatar);
            viewInstance.NextRandomButton.onClick.AddListener(NextRandomAvatar);
        }

#region MainFlow
        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            CheckValidIdentityAndStartInitialFlowAsync().Forget();
            BlockUnwantedInputs();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();

            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Chat_Volume);
            // Unregistering in case player re-login midgame.
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent += OnContinuousAudioStarted;
            InitMusicMute();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            viewInstance!.FinalizeContainer.SetActive(false);
            viewInstance!.JumpIntoWorldButton.interactable = true;
            web3Authenticator.SetVerificationListener(null);
            web3Authenticator.SetOtpRequestListener(null);

            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        private async UniTaskVoid CheckValidIdentityAndStartInitialFlowAsync()
        {
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            // AUTO-LOGIN DISABLED: Always show method selection screen
            /*
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
                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (cached)");
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Profile not found during cached authentication", e);
                    SwitchState(ViewState.Login);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during cached authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
            }
            else
            */
            {
                sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                SwitchState(ViewState.Login);
            }

            if (splashScreen != null) // Splash screen is destroyed after first login
                splashScreen.Hide();
        }

        private void ShowRestrictedUserPopup()
        {
            viewInstance!.RestrictedUserContainer.SetActive(true);
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

        private void StartDappLoginFlowUntilEnd()
        {
            CancelLoginProcess();
            compositeWeb3Provider.CurrentMethod = AuthMethod.DappWallet;

            // Checks the current screen mode because it could have been overridden with Alt+Enter
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();

            loginCancellationToken = new CancellationTokenSource();
            StartDappWalletLoginFlowAsync(loginCancellationToken.Token).Forget();

            return;

            async UniTaskVoid StartDappWalletLoginFlowAsync(CancellationToken ct)
            {
                try
                {
                    CurrentRequestID = string.Empty;

                    viewInstance!.ErrorPopupRoot.SetActive(false);
                    viewInstance!.LoadingSpinner.SetActive(true);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(false);

                    var web3AuthSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "Web3Authentication",
                        SpanOperation = "auth.web3_login",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(web3AuthSpan);

                    web3Authenticator.SetVerificationListener(ShowVerification);

                    IWeb3Identity identity = await web3Authenticator.LoginAsync("", ct);

                    web3Authenticator.SetVerificationListener(null);

                    var identityValidationSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "IdentityValidation",
                        SpanOperation = "auth.identity_validation",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(identityValidationSpan);

                    if (IsUserAllowedToAccessToBeta(identity))
                    {
                        CurrentState.Value = AuthenticationStatus.FetchingProfile;
                        SwitchState(ViewState.Loading);

                        var profileFetchSpan = new SpanData
                        {
                            TransactionName = LOADING_TRANSACTION_NAME,
                            SpanName = "FetchProfile",
                            SpanOperation = "auth.profile_fetch",
                            Depth = 1,
                        };

                        sentryTransactionManager.StartSpan(profileFetchSpan);

                        await FetchProfileAsync(ct);

                        sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                        CurrentState.Value = AuthenticationStatus.LoggedIn;
                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (main)");
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    SwitchState(ViewState.Login);
                }
                catch (SignatureExpiredException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Web3SignatureException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (CodeVerificationException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User profile not found", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    ShowConnectionErrorPopup();
                }
                finally
                {
                    RestoreResolutionAndScreenMode();
                }
            }
        }

        private void CancelLoginAndRestartFromBeginning()
        {
            CancelLoginProcess();
            SwitchState(ViewState.Login);
        }

        private void ShowVerification(int code, DateTime expiration, string requestID)
        {
            viewInstance!.OTPInputField.gameObject.SetActive(false);
            viewInstance!.RegisterButton.gameObject.SetActive(false);
            viewInstance!.VerificationDescriptionsLabel.gameObject.SetActive(true);
            viewInstance!.VerificationCodeLabel.gameObject.SetActive(true);

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

            CurrentState.Value = AuthenticationStatus.VerificationInProgress;
            SwitchState(ViewState.LoginInProgress);
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
                SwitchState(ViewState.Login);
            }

            characterPreviewController?.OnHide();
            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            ChangeAccountAsync(loginCancellationToken.Token).Forget();
        }

        private void JumpIntoWorld()
        {
            viewInstance!.JumpIntoWorldButton.interactable = false;
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                // Restore inputs before transitioning to world
                UnblockUnwantedInputs();

                lifeCycleTask?.TrySetResult();
                lifeCycleTask = null;
            }
        }

        private void SwitchState(ViewState state)
        {
            viewInstance!.ErrorPopupRoot.SetActive(false);

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"[STATUS{CurrentState}]: Changing Auth screen state to {state.ToString()}...");
            switch (state)
            {
                case ViewState.Login:
                    ResetAnimator(viewInstance!.LoginAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance!.VerificationOTP.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);

                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.LoginButton.interactable = true;
                    viewInstance.LoginButton.gameObject.SetActive(true);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    CurrentState.Value = AuthenticationStatus.Login;
                    break;
                case ViewState.Loading:
                    viewInstance!.PendingAuthentication.SetActive(false);
                    viewInstance!.VerificationOTP.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);

                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.LoadingSpinner.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.LoginInProgress:
                    ResetAnimator(viewInstance!.VerificationAnimator);

                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(true);
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.VerificationOTP:
                    ResetAnimator(viewInstance!.VerificationAnimator);

                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(true);

                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);

                    viewInstance.VerificationOTP.SetActive(true);
                    viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.IN);
                    break;
                case ViewState.Finalize:
                    ResetAnimator(viewInstance!.FinalizeAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance!.VerificationOTP.SetActive(false);

                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(true);

                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    viewInstance.JumpIntoWorldButton.interactable = true;

                    characterPreviewController?.OnBeforeShow();
                    characterPreviewController?.OnShow();

                    viewInstance.JumpIntoWorldButton.gameObject.SetActive(true);
                    viewInstance.ProfileNameLabel.gameObject.SetActive(true);
                    viewInstance.Description.SetActive(true);
                    viewInstance.DiffAccountButton.SetActive(true);

                    viewInstance.NewUserContainer.SetActive(false);
                    break;
                case ViewState.FinalizeNewUser:
                    ResetAnimator(viewInstance!.FinalizeAnimator);

                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance!.VerificationOTP.SetActive(false);

                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.LoginButton.gameObject.SetActive(true);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);

                    viewInstance.JumpIntoWorldButton.interactable = true;

                    viewInstance.JumpIntoWorldButton.gameObject.SetActive(false);
                    viewInstance.ProfileNameLabel.gameObject.SetActive(false);
                    viewInstance.Description.SetActive(false);
                    viewInstance.DiffAccountButton.SetActive(false);

                    viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.NewUserContainer.SetActive(true);

                    characterPreviewController?.OnBeforeShow();
                    characterPreviewController?.OnShow();

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Changed screen to {state.ToString()} ✅ ");
        }

        private static void ResetAnimator(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.gameObject.SetActive(false);
        }

        private void RestoreResolutionAndScreenMode()
        {
            Resolution targetResolution = WindowModeUtils.GetTargetResolution(possibleResolutions);
            FullScreenMode targetScreenMode = WindowModeUtils.GetTargetScreenMode(appArgs.HasFlag(AppArgsFlags.WINDOWED_MODE));
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }

        private void CancelLoginProcess()
        {
            Debug.Log("VVV Canceling login");
            loginCancellationToken?.SafeCancelAndDispose();
            loginCancellationToken = null;

            otpCompletionSource?.TrySetCanceled();
            otpCompletionSource = null;
            web3Authenticator.SetOtpRequestListener(null);
        }

        private void OpenOrCloseVerificationCodeHint()
        {
            viewInstance!.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
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

        private void OnContinuousAudioStarted(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.GetInstanceID() != backgroundMusic.GetInstanceID())
                return;

            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
            InitMusicMute();
        }

        private void InitMusicMute()
        {
            bool isMuted = DCLPlayerPrefs.GetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, false);

            if (isMuted)
                UIAudioEventsBus.Instance.SendMuteContinuousAudioEvent(backgroundMusic, true);

            viewInstance?.MuteButton.SetIcon(isMuted);
        }

        private void OnMuteButtonClicked()
        {
            bool isMuted = DCLPlayerPrefs.GetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, false);

            if (isMuted)
                UIAudioEventsBus.Instance.SendMuteContinuousAudioEvent(backgroundMusic, false);
            else
                UIAudioEventsBus.Instance.SendMuteContinuousAudioEvent(backgroundMusic, true);

            viewInstance?.MuteButton.SetIcon(!isMuted);

            DCLPlayerPrefs.SetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, !isMuted, save: true);
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

        private void ShowConnectionErrorPopup() =>
            viewInstance!.ErrorPopupRoot.SetActive(true);

        private void BlockUnwantedInputs() =>
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

        private void UnblockUnwantedInputs() =>
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
#endregion

#region OTP FLOW
        private Profile newUserProfile;
        private void StartOTPLoginFlowUntilEnd()
        {
            CancelLoginProcess();
            compositeWeb3Provider.CurrentMethod = AuthMethod.ThirdWebOTP;

            loginCancellationToken = new CancellationTokenSource();
            StartLoginFlowUntilEndAsync(loginCancellationToken.Token).Forget();

            return;

            async UniTaskVoid StartLoginFlowUntilEndAsync(CancellationToken ct)
            {
                try
                {
                    CurrentRequestID = string.Empty;

                    viewInstance!.ErrorPopupRoot.SetActive(false);
                    viewInstance!.LoadingSpinner.SetActive(true);
                    viewInstance.LoginButton.interactable = false;

                    var web3AuthSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "Web3Authentication",
                        SpanOperation = "auth.web3_login",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(web3AuthSpan);

                    // Set up OTP callback for ThirdWeb OTP flow
                    web3Authenticator.SetOtpRequestListener(RequestOtpFromUserAsync);

                    string email = viewInstance!.EmailInputField.text;
                    currentEmail = email;

                    viewInstance!.OTPInputField.gameObject.SetActive(true);
                    viewInstance!.RegisterButton.gameObject.SetActive(true);
                    viewInstance!.VerificationDescriptionsLabel.gameObject.SetActive(false);
                    viewInstance!.VerificationCodeLabel.gameObject.SetActive(false);

                    // var verificationSpan = new SpanData
                    // {
                    //     TransactionName = LOADING_TRANSACTION_NAME,
                    //     SpanName = "CodeVerification",
                    //     SpanOperation = "auth.code_verification",
                    //     Depth = 1
                    // };
                    // sentryTransactionManager.StartSpan(verificationSpan);
                    // CancelVerificationCountdown();
                    // verificationCountdownCancellationToken = new CancellationTokenSource();
                    // viewInstance.StartVerificationCountdownAsync(expiration,
                    //                  verificationCountdownCancellationToken.Token)
                    //             .Forget();

                    CurrentState.Value = AuthenticationStatus.VerificationInProgress;
                    SwitchState(ViewState.VerificationOTP);

                    IWeb3Identity identity = await web3Authenticator.LoginAsync(email, ct);

                    // Clean up OTP callback
                    web3Authenticator.SetOtpRequestListener(null);
                    otpCompletionSource = null;

                    var identityValidationSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "IdentityValidation",
                        SpanOperation = "auth.identity_validation",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(identityValidationSpan);

                    if (IsUserAllowedToAccessToBeta(identity))
                    {
                        CurrentState.Value = AuthenticationStatus.FetchingProfile;

                        // SwitchState(ViewState.Loading);

                        var profileFetchSpan = new SpanData
                        {
                            TransactionName = LOADING_TRANSACTION_NAME,
                            SpanName = "FetchProfile",
                            SpanOperation = "auth.profile_fetch",
                            Depth = 1,
                        };

                        sentryTransactionManager.StartSpan(profileFetchSpan);

                        var walletAddress = identity.Address.ToString();
                        bool profileExists = await CheckProfileExistsAsync(walletAddress, ct);
                        bool isNewUser = !profileExists && ThirdWebManager.Instance.ActiveWallet != null;

                        if (isNewUser)
                        {
                            IWeb3Identity? identity1 = storedIdentityProvider.Identity;

                            if (identity1 == null)
                                throw new Web3IdentityMissingException("Web3 identity is not available when creating a default profile");

                            // Load base wearables catalog for randomization
                            await LoadBaseWearablesAsync(ct);

                            newUserProfile = BuildDefaultProfile(identity1.Address.ToString(), currentEmail);
                            newUserProfile.HasConnectedWeb3 = true;

                            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
                            InitializeAvatarHistory(newUserProfile.Avatar);
                            sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                            CurrentState.Value = AuthenticationStatus.LoggedIn;
                            SwitchState(ViewState.FinalizeNewUser);
                        }
                        else
                        {
                            Profile? profile = await selfProfile.ProfileAsync(ct);

                            profile!.IsDirty = true;
                            profile.HasConnectedWeb3 = true;

                            profileNameLabel!.Value = profile.Version == 1 ? profile.Name : "back " + profile.Name;
                            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);

                            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
                            sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                            CurrentState.Value = AuthenticationStatus.LoggedIn;
                            SwitchState(ViewState.Finalize);
                        }
                    }
                    else
                    {
                        sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (main)");
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    SwitchState(ViewState.Login);
                }
                catch (SignatureExpiredException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Web3SignatureException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (CodeVerificationException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User profile not found", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    ShowConnectionErrorPopup();
                }
                finally { RestoreResolutionAndScreenMode(); }
            }
        }

        private void SendRegistration(string otp)
        {
            // If we're waiting for OTP input, complete the task with the entered code
            if (otpCompletionSource == null) return;

            // string? otp = viewInstance!.OTPInputField.Code;
            otpCompletionSource.TrySetResult(otp);
        }

        private UniTask<string> RequestOtpFromUserAsync(CancellationToken ct)
        {
            otpCompletionSource = new UniTaskCompletionSource<string>();

            // Register cancellation to clean up if cancelled
            ct.Register(() =>
            {
                otpCompletionSource?.TrySetCanceled(ct);
                otpCompletionSource = null;
            });

            return otpCompletionSource.Task;
        }

        private Profile BuildDefaultProfile(string walletAddress, string name = "")
        {
            // Randomize body shape between MALE and FEMALE
            Avatar avatar = CreateDefaultAvatar();

            // Extract name from email (everything before @) or use default
            var profile = Profile.Create(walletAddress, name, avatar);
            profile.HasClaimedName = false;
            profile.HasConnectedWeb3 = true;
            profile.Description = string.Empty;
            profile.Country = string.Empty;
            profile.EmploymentStatus = string.Empty;
            profile.Gender = string.Empty;
            profile.Pronouns = string.Empty;
            profile.RelationshipStatus = string.Empty;
            profile.SexualOrientation = string.Empty;
            profile.Language = string.Empty;
            profile.Profession = string.Empty;
            profile.RealName = string.Empty;
            profile.Hobbies = string.Empty;
            profile.TutorialStep = 0;
            profile.Version = 0;
            profile.UserNameColor = NameColorHelper.GetNameColor(profile.DisplayName);
            profile.IsDirty = true;

            return profile;
        }

        private Avatar CreateDefaultAvatar()
        {
            BodyShape bodyShape = UnityEngine.Random.value > 0.5f ? BodyShape.MALE : BodyShape.FEMALE;

            // If base wearables loaded from backend - use randomizer
            if (baseWearablesLoaded && maleWearablesByCategory != null && femaleWearablesByCategory != null)
            {
                Dictionary<string, List<URN>>? wearablesByCategory = bodyShape.Equals(BodyShape.MALE) ? maleWearablesByCategory : femaleWearablesByCategory;
                HashSet<URN> wearablesSet = GetRandomWearablesFromCategories(wearablesByCategory);

                return new Avatar(
                    bodyShape,
                    wearablesSet,
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor());
            }

            // Fallback to hardcoded defaults
            return new Avatar(
                bodyShape,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(bodyShape),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());
        }

        private static HashSet<URN> GetRandomWearablesFromCategories(Dictionary<string, List<URN>> wearablesByCategory)
        {
            var result = new HashSet<URN>();

            foreach (List<URN>? categoryWearables in wearablesByCategory.Values)
            {
                if (categoryWearables.Count > 0)
                    result.Add(categoryWearables[UnityEngine.Random.Range(0, categoryWearables.Count)]);
            }

            return result;
        }

        private async UniTask LoadBaseWearablesAsync(CancellationToken ct)
        {
            if (baseWearablesLoaded)
                return;

            try
            {
                // Load base wearables catalog from backend (pageSize 300 to get all)
                (IReadOnlyList<ITrimmedWearable> wearables, _) = await wearablesProvider.GetAsync(
                    pageSize: 300,
                    pageNumber: 1,
                    ct,
                    collectionType: IWearablesProvider.CollectionType.Base);

                maleWearablesByCategory = new Dictionary<string, List<URN>>();
                femaleWearablesByCategory = new Dictionary<string, List<URN>>();

                foreach (ITrimmedWearable? wearable in wearables)
                {
                    string category = wearable.GetCategory();

                    // Skip body shapes
                    if (category == "body_shape")
                        continue;

                    // Add to male dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.MALE))
                    {
                        if (!maleWearablesByCategory.ContainsKey(category))
                            maleWearablesByCategory[category] = new List<URN>();

                        maleWearablesByCategory[category].Add(wearable.GetUrn());
                    }

                    // Add to female dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.FEMALE))
                    {
                        if (!femaleWearablesByCategory.ContainsKey(category))
                            femaleWearablesByCategory[category] = new List<URN>();

                        femaleWearablesByCategory[category].Add(wearable.GetUrn());
                    }
                }

                baseWearablesLoaded = true;
                ReportHub.Log(ReportCategory.AUTHENTICATION, $"Base wearables catalog loaded: {wearables.Count} items, male categories: {maleWearablesByCategory.Count}, female categories: {femaleWearablesByCategory.Count}");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                // Fallback to hardcoded defaults will be used
                baseWearablesLoaded = false;
            }
        }

        private void FinalizeNewUser()
        {
            PublishNewProfile(loginCancellationToken.Token).Forget();

            async UniTaskVoid PublishNewProfile(CancellationToken ct)
            {
                newUserProfile.Name = viewInstance.ProfileNameInputField.text;
                Profile? publishedProfile = await selfProfile.UpdateProfileAsync(newUserProfile, ct, updateAvatarInWorld: false);
                newUserProfile = publishedProfile ?? throw new ProfileNotFoundException();
                JumpIntoWorld();
            }
        }

        private static string ExtractNameFromEmail(string? email)
        {
            if (string.IsNullOrEmpty(email))
                return IProfileRepository.PLAYER_RANDOM_ID;

            int atIndex = email.IndexOf('@');

            if (atIndex <= 0)
                return IProfileRepository.PLAYER_RANDOM_ID;

            return email[..atIndex];
        }
#endregion

#region AVATAR HISTORY NAVIGATION
        private readonly List<Avatar> avatarHistory = new ();
        private int currentAvatarIndex = -1;

        private void InitializeAvatarHistory(Avatar initialAvatar)
        {
            avatarHistory.Clear();
            avatarHistory.Add(initialAvatar);
            currentAvatarIndex = 0;
            UpdateAvatarNavigationButtons();
        }

        private void RandomizeAvatar()
        {
            // If we're not at the end of history, remove all avatars after current position
            if (currentAvatarIndex < avatarHistory.Count - 1)
                avatarHistory.RemoveRange(currentAvatarIndex + 1, avatarHistory.Count - currentAvatarIndex - 1);

            // Create and add new avatar to history
            Avatar newAvatar = CreateDefaultAvatar();
            avatarHistory.Add(newAvatar);
            currentAvatarIndex = avatarHistory.Count - 1;

            // Apply to profile and preview
            newUserProfile.Avatar = newAvatar;
            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnShow();

            UpdateAvatarNavigationButtons();
        }

        private void PrevRandomAvatar()
        {
            if (currentAvatarIndex <= 0)
                return;

            currentAvatarIndex--;
            ApplyAvatarFromHistory();
            UpdateAvatarNavigationButtons();
        }

        private void NextRandomAvatar()
        {
            if (currentAvatarIndex >= avatarHistory.Count - 1)
                return;

            currentAvatarIndex++;
            ApplyAvatarFromHistory();
            UpdateAvatarNavigationButtons();
        }

        private void ApplyAvatarFromHistory()
        {
            Avatar avatar = avatarHistory[currentAvatarIndex];
            newUserProfile.Avatar = avatar;
            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnShow();
        }

        private void UpdateAvatarNavigationButtons()
        {
            if (viewInstance == null)
                return;

            viewInstance.PrevRandomButton.interactable = currentAvatarIndex > 0;
            viewInstance.NextRandomButton.interactable = currentAvatarIndex < avatarHistory.Count - 1;
        }
#endregion

        /// <summary>
        ///     Проверяет существование профиля через GET запрос к API Decentraland.
        /// </summary>
        /// <param name="walletAddress">Ethereum адрес кошелька</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>true если профиль существует (200), false если не существует (404) или ошибка</returns>
        private async UniTask<bool> CheckProfileExistsAsync(string walletAddress, CancellationToken ct)
        {
            const string PROFILES_API_URL = "https://peer.decentraland.org/lambdas/profiles/";
            var url = $"{PROFILES_API_URL}{walletAddress}";

            try
            {
                int statusCode = await webRequestController
                                      .GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.PROFILE)
                                      .StatusCodeAsync();

                return statusCode == 200;
            }
            catch (Exception) { return false; }
        }

    }
}
