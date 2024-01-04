using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3Authentication;
using MVC;
using System;
using System.Threading;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private const string DISCORD_LINK = "https://decentraland.org/discord/";

        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IProfileRepository profileRepository;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache storedIdentityProvider;

        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IProfileRepository profileRepository,
            IWebBrowser webBrowser,
            IWeb3IdentityCache storedIdentityProvider)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.profileRepository = profileRepository;
            this.webBrowser = webBrowser;
            this.storedIdentityProvider = storedIdentityProvider;
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
            CancelVerificationCountdown();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileNameLabel = (StringVariable)viewInstance.ProfileNameLabel.StringReference["profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartFlow);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
            viewInstance.UseAnotherAccountButton.onClick.AddListener(RestartLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);

            web3Authenticator.AddVerificationListener((code, expiration) =>
            {
                viewInstance.VerificationCodeLabel.text = code.ToString();

                CancelVerificationCountdown();
                verificationCountdownCancellationToken = new CancellationTokenSource();

                viewInstance.StartVerificationCountdown(expiration,
                                 verificationCountdownCancellationToken.Token)
                            .Forget();

                SwitchState(ViewState.LoginInProgress);
            });
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            ShowSplashAndThenSwitchToLoginAsync().Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
            CancelVerificationCountdown();
        }

        private async UniTaskVoid ShowSplashAndThenSwitchToLoginAsync()
        {
            SwitchState(ViewState.Splash);

            viewInstance.SplashVideoPlayer.Play();

            await UniTask.WaitUntil(() => viewInstance.SplashVideoPlayer.frame >= (long)(viewInstance.SplashVideoPlayer.frameCount - 1));

            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            if (storedIdentity is { IsExpired: false })
            {
                SwitchState(ViewState.Loading);

                CancelLoginProcess();
                loginCancellationToken = new CancellationTokenSource();
                await FetchProfileAsync(storedIdentity, loginCancellationToken.Token);

                SwitchState(ViewState.Finalize);
            }
            else
                SwitchState(ViewState.Login);
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            (lifeCycleTask ??= new UniTaskCompletionSource()).Task.AttachExternalCancellation(ct);

        private void StartFlow()
        {
            async UniTaskVoid StartFlowAsync(CancellationToken ct)
            {
                try
                {
                    viewInstance.ConnectingToServerContainer.SetActive(true);
                    viewInstance.LoginButton.interactable = false;

                    IWeb3Identity web3Identity = await web3Authenticator.LoginAsync(ct);

                    SwitchState(ViewState.Loading);

                    await FetchProfileAsync(web3Identity, ct);

                    SwitchState(ViewState.Finalize);
                }
                catch (Exception e)
                {
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            StartFlowAsync(loginCancellationToken.Token).Forget();
        }

        private async UniTask FetchProfileAsync(IWeb3Identity web3Identity, CancellationToken ct)
        {
            // TODO: get latest profile version from storage if any (?)
            Profile? profile = await profileRepository.GetAsync(web3Identity.Address, 0, ct);
            profileNameLabel!.Value = profile?.Name;
        }

        private void RestartLoginProcess()
        {
            CancelLoginProcess();
            SwitchState(ViewState.Login);
        }

        private void JumpIntoWorld()
        {
            lifeCycleTask!.TrySetResult();
            lifeCycleTask = null;
        }

        private void SwitchState(ViewState state)
        {
            switch (state)
            {
                case ViewState.Splash:
                    viewInstance.SplashContainer.SetActive(true);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    break;
                case ViewState.Login:
                    viewInstance.SplashContainer.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    break;
                case ViewState.LoginInProgress:
                    viewInstance.SplashContainer.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Loading:
                    viewInstance.SplashContainer.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Finalize:
                    viewInstance.SplashContainer.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
            }
        }

        private void CancelLoginProcess()
        {
            loginCancellationToken?.SafeCancelAndDispose();
            loginCancellationToken = null;
        }

        private void OpenOrCloseVerificationCodeHint()
        {
            viewInstance.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
        }

        private void OpenDiscord() =>
            webBrowser.OpenUrl(DISCORD_LINK);

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private enum ViewState
        {
            Splash,
            Login,
            LoginInProgress,
            Loading,
            Finalize,
        }
    }
}
