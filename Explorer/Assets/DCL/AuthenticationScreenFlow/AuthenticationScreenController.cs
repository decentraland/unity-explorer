using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private const int STARTING_VIEW_SIBLING_INDEX = 2;
        private const int ANIMATION_DELAY = 300;

        private static readonly int IN = Animator.StringToHash("In");
        private static readonly int OUT = Animator.StringToHash("Out");
        private static readonly int JUMP_IN = Animator.StringToHash("Jump");
        private static readonly int TO_OTHER = Animator.StringToHash("Different");

        private enum ViewState
        {
            Login,
            LoginInProgress,
            Loading,
            Finalize,
        }

        private const string DISCORD_LINK = "https://decentraland.org/discord/";

        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly Animator splashScreenAnimator;

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;
        private World? world;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(
            ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            Animator splashScreenAnimator)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreenAnimator = splashScreenAnimator;
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

            profileNameLabel = (StringVariable)viewInstance.ProfileNameLabel.StringReference["profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartLoginFlowUntilEnd);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
            viewInstance.UseAnotherAccountButton.onClick.AddListener(ChangeAccount);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.VersionText.text = Application.version;
#if UNITY_EDITOR
            viewInstance.VersionText.text = "editor-version";
#endif

            Assert.IsNotNull(world);
            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world!);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            viewInstance.gameObject.transform.SetSiblingIndex(STARTING_VIEW_SIBLING_INDEX);
            CheckValidIdentityAndStartInitialFlowAsync().Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            viewInstance.FinalizeContainer.SetActive(false);
            web3Authenticator.SetVerificationListener(null);
        }

        private async UniTaskVoid CheckValidIdentityAndStartInitialFlowAsync()
        {
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            if (storedIdentity is { IsExpired: false })
            {
                CancelLoginProcess();
                loginCancellationToken = new CancellationTokenSource();
                await FetchProfileAsync(loginCancellationToken.Token);

                SwitchState(ViewState.Finalize);
            }
            else
                SwitchState(ViewState.Login);

            splashScreenAnimator.SetTrigger(OUT);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            (lifeCycleTask ??= new UniTaskCompletionSource()).Task.AttachExternalCancellation(ct);

        private void StartLoginFlowUntilEnd()
        {
            async UniTaskVoid StartLoginFlowUntilEndAsync(CancellationToken ct)
            {
                try
                {
                    viewInstance.ConnectingToServerContainer.SetActive(true);
                    viewInstance.LoginButton.interactable = false;

                    web3Authenticator.SetVerificationListener(ShowVerification);

                    await web3Authenticator.LoginAsync(ct);

                    web3Authenticator.SetVerificationListener(null);

                    SwitchState(ViewState.Loading);

                    await FetchProfileAsync(ct);

                    SwitchState(ViewState.Finalize);
                }
                catch (OperationCanceledException)
                {
                    SwitchState(ViewState.Login);
                }
                catch (Exception e)
                {
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            StartLoginFlowUntilEndAsync(loginCancellationToken.Token).Forget();
        }

        private void ShowVerification(int code, DateTime expiration)
        {
            viewInstance.VerificationCodeLabel.text = code.ToString();

            CancelVerificationCountdown();
            verificationCountdownCancellationToken = new CancellationTokenSource();

            viewInstance.StartVerificationCountdownAsync(expiration,
                             verificationCountdownCancellationToken.Token)
                        .Forget();

            SwitchState(ViewState.LoginInProgress);
        }

        private async UniTask FetchProfileAsync(CancellationToken ct)
        {
            Profile profile = await selfProfile.ProfileOrPublishIfNotAsync(ct);
            profileNameLabel!.Value = profile.Name;
            characterPreviewController?.Initialize(profile.Avatar);
        }

        private void ChangeAccount()
        {
            async UniTaskVoid ChangeAccountAsync(CancellationToken ct)
            {
                viewInstance.FinalizeAnimator.SetTrigger(TO_OTHER);
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
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                viewInstance.FinalizeAnimator.SetTrigger(JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();
                lifeCycleTask?.TrySetResult();
                lifeCycleTask = null;
            }
            AnimateAndAwaitAsync().Forget();
        }

        private void SwitchState(ViewState state)
        {
            switch (state)
            {
                case ViewState.Login:
                    ResetAnimator(viewInstance.LoginAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    break;
                case ViewState.LoginInProgress:
                    ResetAnimator(viewInstance.VerificationAnimator);
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(OUT);
                    viewInstance.VerificationAnimator.SetTrigger(IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Loading:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Finalize:
                    ResetAnimator(viewInstance.FinalizeAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.FinalizeAnimator.SetTrigger(IN);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
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
            viewInstance.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
        }

        private void OpenDiscord() =>
            webBrowser.OpenUrl(DISCORD_LINK);

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        public void SetWorld(World world)
        {
            this.world = world;
        }
    }
}
