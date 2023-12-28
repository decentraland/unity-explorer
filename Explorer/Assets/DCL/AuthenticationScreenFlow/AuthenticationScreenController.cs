using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3Authentication;
using MVC;
using System;
using System.Threading;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IProfileRepository profileRepository;

        private CancellationTokenSource? loginCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IProfileRepository profileRepository)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.profileRepository = profileRepository;
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.LoginButton.onClick.AddListener(StartFlow);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
            viewInstance.UseAnotherAccountButton.onClick.AddListener(RestartLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);

            web3Authenticator.AddVerificationListener((code, expiration) =>
            {
                viewInstance.VerificationCodeLabel.text = code.ToString();
                SwitchState(ViewState.LoginInProgress);
            });
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            SwitchState(ViewState.Login);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
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
                catch (Exception e) { SwitchState(ViewState.Login); }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            StartFlowAsync(loginCancellationToken.Token).Forget();
        }

        private async UniTask FetchProfileAsync(IWeb3Identity web3Identity, CancellationToken ct)
        {
            // TODO: get latest profile version from storage if any (?)
            Profile? profile = await profileRepository.GetAsync(web3Identity.Address, 0, ct);
            var profileNameLabel = viewInstance.ProfileNameLabel.StringReference["profileName"] as StringVariable;
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
                case ViewState.Login:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    break;
                case ViewState.LoginInProgress:
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Loading:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    break;
                case ViewState.Finalize:
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
            try
            {
                loginCancellationToken?.Cancel();
                loginCancellationToken?.Dispose();
            }
            catch (ObjectDisposedException) { }

            loginCancellationToken = null;
        }

        private void OpenOrCloseVerificationCodeHint()
        {
            viewInstance.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
        }

        private enum ViewState
        {
            Login,
            LoginInProgress,
            Loading,
            Finalize,
        }
    }
}
