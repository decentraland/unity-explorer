using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine;
using DCL.FeatureFlags;
using DCL.AvatarRendering.Wearables;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings.Utils;
using DCL.UI;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Global.AppArgs;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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

        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        internal const int ANIMATION_DELAY = 300;
        internal const string LOADING_TRANSACTION_NAME = "loading_process";

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

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private readonly IInputBlock inputBlock;

        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? loginCancellationTokenSource;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;
        public ReactiveProperty<AuthenticationStatus> CurrentState { get; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; internal set; } = string.Empty;

        public event Action DiscordButtonClicked;

        private MVCStateMachine<AuthStateBase> fsm;
        private AuthenticationScreenAudio audio;

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
            IAppArgs appArgs, IWearablesProvider wearablesProvider)
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

            possibleResolutions.AddRange(ResolutionUtils.GetAvailableResolutions());
        }

        public override void Dispose()
        {
            base.Dispose();
            characterPreviewController?.Dispose();

            CancelLoginProcess();
            audio.Dispose();
            fsm.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            audio = new AuthenticationScreenAudio(viewInstance, audioMixerVolumesController, backgroundMusic);
            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, emotesSettings, characterPreviewFactory, world, characterPreviewEventBus);

            bool enableEmailOTP = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.EMAIL_OTP_AUTH);
            viewInstance.LoginSelectionAuthView.EmailOTPContainer.SetActive(enableEmailOTP);

            // Subscriptions
            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.ExitButton.onClick.AddListener(ExitApplication);

            // States
            fsm = new MVCStateMachine<AuthStateBase>();

            fsm.AddStates(
                new InitAuthState(viewInstance, buildData.InstallSource),

                new LoginSelectionAuthState(fsm, viewInstance, this, CurrentState, splashScreen, compositeWeb3Provider),
                new ProfileFetchingAuthState(fsm, viewInstance, this, CurrentState, sentryTransactionManager, selfProfile),
                new IdentityVerificationDappAuthState(fsm, viewInstance, this, CurrentState, web3Authenticator, appArgs, possibleResolutions, sentryTransactionManager),
                new LobbyForExistingAccountAuthState(fsm, viewInstance, this, splashScreen, CurrentState, characterPreviewController)
            );

            if (enableEmailOTP)
            {
                fsm.AddStates(
                    new ProfileFetchingOTPAuthState(fsm, viewInstance, this, CurrentState, sentryTransactionManager, selfProfile),
                    new IdentityVerificationOTPAuthState(fsm, viewInstance, this, CurrentState, web3Authenticator, sentryTransactionManager),
                    new LobbyForNewAccountAuthState(fsm, viewInstance, this, CurrentState, characterPreviewController, selfProfile, wearablesProvider)
                );
            }

            fsm.Enter<InitAuthState>();
        }

        /// <summary>
        /// First time view is shown from bootstrap and could have storedIdentity.
        /// In all other cases view is shown after logout.
        /// </summary>
        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            // Force to re-login if the identity will expire in 24hs or less, so we mitigate the chances on
            // getting the identity expired while in-world, provoking signed-fetch requests to fail
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;
            if (storedIdentity is { IsExpired: false } && storedIdentity.Expiration - DateTime.UtcNow > TimeSpan.FromDays(1))
            {
                CancelLoginProcess();
                loginCancellationTokenSource = new CancellationTokenSource();

                web3Authenticator.TryAutoConnectAsync(loginCancellationTokenSource.Token).Forget();
                fsm.Enter<ProfileFetchingAuthState, (IWeb3Identity identity, bool isCached, CancellationToken ct)>((storedIdentity, true, loginCancellationTokenSource.Token));
            }
            else
            {
                sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                fsm.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, UIAnimationHashes.IN), allowReEnterSameState: true);
            }
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();

            BlockUnwantedInputs();
            audio.OnShow();

            // Setup transaction confirmation callback for ThirdWeb (Instance is guaranteed to exist at this point)
            SetupTransactionConfirmationCallback();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            fsm.CurrentState?.Exit();
            CancelLoginProcess();

            UnblockUnwantedInputs();
            audio.OnHide();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask?.TrySetCanceled(ct);
            lifeCycleTask = new UniTaskCompletionSource();
            await lifeCycleTask.Task;
        }

        internal void TrySetLifeCycle()
        {
            lifeCycleTask?.TrySetResult();
            lifeCycleTask = null;
        }

        internal void CancelLoginProcess()
        {
            loginCancellationTokenSource?.SafeCancelAndDispose();
            loginCancellationTokenSource = null;
        }

        internal CancellationToken GetRestartedLoginToken()
        {
            loginCancellationTokenSource = loginCancellationTokenSource.SafeRestart();
            return loginCancellationTokenSource.Token;
        }

        public void ChangeAccount()
        {
            ChangeAccountAsync(GetRestartedLoginToken()).Forget();
            return;

            async UniTaskVoid ChangeAccountAsync(CancellationToken ct)
            {
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                await web3Authenticator.LogoutAsync(ct);
                characterPreviewController?.OnHide();
                fsm.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, UIAnimationHashes.SLIDE), allowReEnterSameState: true);
            }
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

        private void RequestAlphaAccess() =>
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);

        private void BlockUnwantedInputs() =>
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

        private void UnblockUnwantedInputs() =>
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

        private void SetupTransactionConfirmationCallback()
        {
            viewInstance.TransactionFeeConfirmationView.SetDrawOrder(new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, 500));
            viewInstance.TransactionFeeConfirmationView!.transform.parent = null;
            compositeWeb3Provider.SetTransactionConfirmationCallback(viewInstance.TransactionFeeConfirmationView.ShowAsync);
        }
    }
}
