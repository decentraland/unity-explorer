using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Prefs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings.Utils;
using DCL.UI;
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
        private const float WINDOWED_RESOLUTION_RESIZE_COEFFICIENT = .75f;
        private const FullScreenMode DEFAULT_SCREEN_MODE = FullScreenMode.FullScreenWindow;

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
            AudioClipConfig backgroundMusic)
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

            if (storedIdentity is { IsExpired: false }

                // Force to re-login if the identity will expire in 24hs or less, so we mitigate the chances on
                // getting the identity expired while in-world, provoking signed-fetch requests to fail
                && storedIdentity.Expiration - DateTime.UtcNow > TimeSpan.FromDays(1))
            {
                CancelLoginProcess();
                loginCancellationToken = new CancellationTokenSource();

                try
                {
                    if (IsUserAllowedToAccessToBeta(storedIdentity))
                    {
                        CurrentState.Value = AuthenticationStatus.FetchingProfileCached;

                        await FetchProfileAsync(loginCancellationToken.Token);

                        CurrentState.Value = AuthenticationStatus.LoggedInCached;
                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (ProfileNotFoundException) { SwitchState(ViewState.Login); }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
            }
            else
                SwitchState(ViewState.Login);

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

            if (!flags.IsEnabled(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLETS_VARIANT)) return true;

            if (!flags.TryGetCsvPayload(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLETS_VARIANT, out List<List<string>>? allowedUsersCsv))
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
            ForceResolutionAndWindowedMode();

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

                    web3Authenticator.SetVerificationListener(ShowVerification);

                    IWeb3Identity identity = await web3Authenticator.LoginAsync(ct);

                    web3Authenticator.SetVerificationListener(null);

                    if (IsUserAllowedToAccessToBeta(identity))
                    {
                        CurrentState.Value = AuthenticationStatus.FetchingProfile;
                        SwitchState(ViewState.Loading);

                        await FetchProfileAsync(ct);

                        CurrentState.Value = AuthenticationStatus.LoggedIn;
                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (OperationCanceledException) { SwitchState(ViewState.Login); }
                catch (SignatureExpiredException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Web3SignatureException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (CodeVerificationException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (ProfileNotFoundException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
                catch (Exception e)
                {
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

        private void ForceResolutionAndWindowedMode()
        {
            Resolution current = Screen.currentResolution;

            int targetWidth = (int)(current.width * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT);
            int targetHeight = (int)(current.height * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT);

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed, current.refreshRateRatio);
        }

        private void RestoreResolutionAndScreenMode()
        {
            Resolution targetResolution = GetTargetResolution();
            FullScreenMode targetScreenMode = GetTargetScreenMode();
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }

        private Resolution GetTargetResolution()
        {
            return DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_RESOLUTION)
                ? GetSavedResolution()
                : GetDefaultResolution();

            Resolution GetSavedResolution()
            {
                int index = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_RESOLUTION);
                return possibleResolutions[index];
            }

            Resolution GetDefaultResolution()
            {
                int defaultIndex = 0;

                for (var index = 0; index < possibleResolutions.Count; index++)
                {
                    Resolution resolution = possibleResolutions[index];

                    if (!ResolutionUtils.IsDefaultResolution(resolution))
                        continue;

                    defaultIndex = index;
                    break;
                }

                return possibleResolutions[defaultIndex];
            }
        }

        private FullScreenMode GetTargetScreenMode()
        {
            return DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WINDOW_MODE) ? GetSavedScreenMode() : DEFAULT_SCREEN_MODE;

            FullScreenMode GetSavedScreenMode()
            {
                int index = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE);
                return FullscreenModeUtils.Modes[index];
            }
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
