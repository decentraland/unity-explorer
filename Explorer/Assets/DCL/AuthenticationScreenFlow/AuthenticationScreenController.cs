using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

#if !UNITY_EDITOR
using DCL.Web3;
using System.Collections.Generic;
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
        private readonly ISplashScreen splashScreenAnimator;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly BuildData buildData;
#if !UNITY_EDITOR
        private readonly FeatureFlagsCache featureFlagsCache;
#endif
        private readonly AudioMixerVolumesController audioMixerVolumesController;
        private readonly World world;
        private readonly AuthScreenEmotesSettings emotesSettings;

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;
        private float originalWorldAudioVolume;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ReactiveProperty<AuthenticationStatus> CurrentState { get; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; private set; } = string.Empty;

        public AuthenticationScreenController(
            ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache,
            IWebBrowser webBrowser,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            ISplashScreen splashScreenAnimator,
            CharacterPreviewEventBus characterPreviewEventBus,
            AudioMixerVolumesController audioMixerVolumesController,
            BuildData buildData,
            World world,
            AuthScreenEmotesSettings emotesSettings)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.selfProfile = selfProfile;
#if !UNITY_EDITOR
            this.featureFlagsCache = featureFlagsCache;
#endif
            this.webBrowser = webBrowser;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreenAnimator = splashScreenAnimator;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.buildData = buildData;
            this.world = world;
            this.emotesSettings = emotesSettings;
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            characterPreviewController?.Dispose();
            web3Authenticator.SetVerificationListener(null);
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartLoginFlowUntilEnd);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);

            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);

#if UNITY_EDITOR
            viewInstance.VersionText.text = $"editor-version - {buildData.InstallSource}";
#else
            viewInstance.VersionText.text = $"{Application.version} - {buildData.InstallSource}";
#endif
            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, emotesSettings, characterPreviewFactory, world, characterPreviewEventBus);

            viewInstance.ErrorPopupCloseButton.onClick.AddListener(CloseErrorPopup);
            viewInstance.ErrorPopupExitButton.onClick.AddListener(ExitApp);
            viewInstance.ErrorPopupRetryButton.onClick.AddListener(StartLoginFlowUntilEnd);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            CheckValidIdentityAndStartInitialFlowAsync().Forget();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();

            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            viewInstance!.FinalizeContainer.SetActive(false);
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

            splashScreenAnimator.Hide();
        }

        private void ShowRestrictedUserPopup()
        {
            viewInstance!.RestrictedUserContainer.SetActive(true);
        }

        private bool IsUserAllowedToAccessToBeta(IWeb3Identity storedIdentity)
        {
#if UNITY_EDITOR
            return true;
#else
            if (!featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLETS_VARIANT)) return true;
            if (!featureFlagsCache.Configuration.TryGetCsvPayload(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLETS_VARIANT, out List<List<string>>? allowedUsersCsv))
                return true;

            bool isUserAllowed = allowedUsersCsv![0]
               .Exists(s => new Web3Address(s).Equals(storedIdentity.Address));

            return isUserAllowed;
#endif
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
                    viewInstance!.ConnectingToServerContainer.SetActive(true);
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
                catch (SignatureExpiredException) { SwitchState(ViewState.Login); }
                catch (ProfileNotFoundException) { SwitchState(ViewState.Login); }
                catch (Exception e)
                {
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    ShowConnectionErrorPopup();
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
            profileNameLabel!.Value = profile.Name;
            characterPreviewController?.Initialize(profile.Avatar);
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
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitIt() ?? UniTask.CompletedTask);
                characterPreviewController?.OnHide();
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
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    viewInstance.RestrictedUserContainer.SetActive(false);

                    CurrentState.Value = AuthenticationStatus.Login;
                    break;
                case ViewState.LoginInProgress:
                    ResetAnimator(viewInstance!.VerificationAnimator);
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);
                    viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.Loading:
                    viewInstance!.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.Finalize:
                    ResetAnimator(viewInstance!.FinalizeAnimator);
                    viewInstance.Slides.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void ResetAnimator(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.gameObject.SetActive(false);
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

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private void RequestAlphaAccess() =>
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);

        private void ExitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            return;
#endif
            Application.Quit();
        }

        private void CloseErrorPopup() =>
            viewInstance!.ErrorPopupRoot.SetActive(false);

        private void ShowConnectionErrorPopup() =>
            viewInstance!.ErrorPopupRoot.SetActive(true);
    }
}
