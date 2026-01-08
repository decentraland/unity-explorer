using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine;
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
using Global.AppArgs;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Utility;
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

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private readonly IInputBlock inputBlock;

        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? loginCancellationTokenSource;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.FULLSCREEN;
        public ReactiveProperty<AuthenticationStatus> CurrentState { get; } = new (AuthenticationStatus.Init);
        public string CurrentRequestID { get; internal set; } = string.Empty;

        public event Action DiscordButtonClicked;

        private MVCStateMachine<AuthStateBase> fsm;
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

            // Subscriptions
            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.ExitButton.onClick.AddListener(ExitApplication);

            // States
            fsm = new MVCStateMachine<AuthStateBase>();
            fsm.AddStates(
                new InitAuthScreenState(viewInstance, buildData.InstallSource),
                new LoginStartAuthState(fsm, viewInstance, this, CurrentState, splashScreen),
                new IdentityAndVerificationAuthState(fsm, viewInstance, this, CurrentState, web3Authenticator, appArgs, possibleResolutions, sentryTransactionManager),
                new ProfileFetchingAuthState(fsm, viewInstance, this, CurrentState, sentryTransactionManager, splashScreen, characterPreviewController, selfProfile),
                new LobbyAuthState(viewInstance, this, characterPreviewController)
                );
            fsm.Enter<InitAuthScreenState>();
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
                fsm.Enter<ProfileFetchingAuthState, (IWeb3Identity identity, bool isCached, CancellationToken ct)>((storedIdentity, true, loginCancellationTokenSource.Token));
            }
            else
            {
                sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
            }
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();

            BlockUnwantedInputs();
            audio.OnShow();
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

        private void ChangeAccount()
        {
            characterPreviewController?.OnHide();

            ChangeAccountAsync(GetRestartedLoginToken()).Forget();
            return;

            async UniTaskVoid ChangeAccountAsync(CancellationToken ct)
            {
                viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.TO_OTHER);
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                await web3Authenticator.LogoutAsync(ct);

                fsm.Enter<LoginStartAuthState>(allowReEnterSameState: true);
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
    }
}
