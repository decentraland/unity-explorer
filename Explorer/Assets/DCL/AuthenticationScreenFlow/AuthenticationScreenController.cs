using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Audio;
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
using Global.AppArgs;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Prefs;
using DCL.Utility;
using Sentry;
using ThirdWebUnity;
using ThirdWebUnity.Playground;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

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
        }

        private const int ANIMATION_DELAY = 300;

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
        private UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;
        private IInputBlock inputBlock;
        private float originalWorldAudioVolume;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ReactiveProperty<AuthenticationStatus> CurrentState { get; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; private set; } = string.Empty;

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
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartLoginFlowUntilEnd);
            viewInstance.RegisterButton.onClick.AddListener(SendRegistration);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
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
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(StartLoginFlowUntilEnd);
        }

        private void SendRegistration()
        {
            _ = ThirdWebCustomJWTAuth.Register(viewInstance.EmailInputField.text, viewInstance.PasswordInputField.text);
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

            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        private async UniTaskVoid CheckValidIdentityAndStartInitialFlowAsync()
        {
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            // AUTO-LOGIN DISABLED: Always show login screen
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

        private void StartLoginFlowUntilEnd()
        {
            CancelLoginProcess();

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

                    string email = viewInstance!.EmailInputField.text;
                    string password = viewInstance!.PasswordInputField.text;

                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                        Debug.Log("ERROR: Email and Password are required");

                    IWeb3Identity identity = await web3Authenticator.LoginAsync(email, password, ct);

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
                finally { RestoreResolutionAndScreenMode(); }
            }
        }

        private void StartLoginFlowUntilEnd2()
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

                    viewInstance!.ErrorPopupRoot.SetActive(false);
                    viewInstance!.LoadingSpinner.SetActive(true);
                    viewInstance.LoginButton.interactable = false;

                    var web3AuthSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "Web3Authentication",
                        SpanOperation = "auth.web3_login",
                        Depth = 1
                    };
                    sentryTransactionManager.StartSpan(web3AuthSpan);

                    web3Authenticator.SetVerificationListener(ShowVerification);

                    IWeb3Identity identity = await web3Authenticator.LoginAsync("", "", ct);

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
                        CurrentState.Value = AuthenticationStatus.FetchingProfile;
                        SwitchState(ViewState.Loading);

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

            CurrentState.Value = AuthenticationStatus.VerificationInProgress;
            SwitchState(ViewState.LoginInProgress);
        }

        private async UniTask FetchProfileAsync(CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null && ThirdWebManager.Instance.ActiveWallet != null)
                profile = await CreateAndPublishDefaultProfileAsync(ct);

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

        private async UniTask<Profile> CreateAndPublishDefaultProfileAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = storedIdentityProvider.Identity;

            if (identity == null)
                throw new Web3IdentityMissingException("Web3 identity is not available when creating a default profile");

            Profile defaultProfile = BuildDefaultProfile(identity.Address.ToString());
            Profile? publishedProfile = await selfProfile.UpdateProfileAsync(defaultProfile, ct, updateAvatarInWorld: false);

            if (publishedProfile == null)
                throw new ProfileNotFoundException();

            return publishedProfile;
        }

        private static Profile BuildDefaultProfile(string walletAddress)
        {
            var avatar = new Profiles.Avatar(
                BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());

            var profile = Profile.Create(walletAddress, IProfileRepository.PLAYER_RANDOM_ID, avatar);
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

            switch (state)
            {
                case ViewState.Login:
                    ResetAnimator(viewInstance!.LoginAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);

                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.LoginButton.interactable = true;

                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);

                    CurrentState.Value = AuthenticationStatus.Login;
                    break;
                case ViewState.Loading:
                    viewInstance!.PendingAuthentication.SetActive(false);

                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.LoadingSpinner.SetActive(true);
                    viewInstance.LoginButton.interactable = true;

                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.LoginInProgress:
                    ResetAnimator(viewInstance!.VerificationAnimator);

                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;

                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.Finalize:
                    ResetAnimator(viewInstance!.FinalizeAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);

                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.LoadingSpinner.SetActive(false);
                    viewInstance.LoginButton.interactable = false;

                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    viewInstance.JumpIntoWorldButton.interactable = true;
                    characterPreviewController?.OnBeforeShow();
                    characterPreviewController?.OnShow();

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
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
            loginCancellationToken?.SafeCancelAndDispose();
            loginCancellationToken = null;
        }

        private void OpenOrCloseVerificationCodeHint()
        {
            viewInstance!.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
        }

        private void OpenDiscord() =>
            webBrowser.OpenUrl(DecentralandUrl.DiscordLink);

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
    }
}
