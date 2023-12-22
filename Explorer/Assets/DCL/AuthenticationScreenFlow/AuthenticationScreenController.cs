using Cysharp.Threading.Tasks;
using DCL.Web3Authentication;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private readonly IWeb3Authenticator web3Authenticator;

        private CancellationTokenSource? loginCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(ViewFactoryMethod viewFactory,
            IWeb3Authenticator web3Authenticator)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
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
                    viewInstance.PendingAuthentication.SetActive(true);

                    await web3Authenticator.LoginAsync(ct);

                    SwitchState(ViewState.Loading);
                    await WaitUntilWorldIsLoadedAsync(ct);

                    SwitchState(ViewState.Finalize);
                }
                catch (Exception) { SwitchState(ViewState.Login); }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            StartFlowAsync(loginCancellationToken.Token).Forget();
        }

        private async UniTask WaitUntilWorldIsLoadedAsync(CancellationToken ct)
        {
            // TODO: make real implementation
            const float DURATION = 3;
            float t = 0;

            viewInstance.ProgressBar.normalizedValue = 0f;

            while (t < DURATION)
            {
                await UniTask.NextFrame(ct);
                t += Time.deltaTime;
                viewInstance.ProgressBar.normalizedValue = Mathf.Clamp01(t / DURATION);
                var progressLabelValue = viewInstance.ProgressLabel.StringReference["progressValue"] as IntVariable;
                progressLabelValue!.Value = (int)(t / DURATION * 100);
            }

            viewInstance.ProgressBar.normalizedValue = 1f;
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
                    viewInstance.LoginButton.gameObject.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    break;
                case ViewState.LoginInProgress:
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.LoginButton.gameObject.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    break;
                case ViewState.Loading:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginButton.gameObject.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    break;
                case ViewState.Finalize:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginButton.gameObject.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(true);
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

        private enum ViewState
        {
            Login,
            LoginInProgress,
            Loading,
            Finalize,
        }
    }
}
