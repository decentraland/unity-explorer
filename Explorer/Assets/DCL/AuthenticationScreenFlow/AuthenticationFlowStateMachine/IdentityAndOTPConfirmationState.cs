using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics;
using DCL.UI;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class IdentityAndOTPConfirmationState : AuthStateBase, IPayloadedState<(string email, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly SentryTransactionManager sentryTransactionManager;

        public IdentityAndOTPConfirmationState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            IWeb3VerifiedAuthenticator web3Authenticator,
            SentryTransactionManager sentryTransactionManager) : base(viewInstance)
        {
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.web3Authenticator = web3Authenticator;
            this.sentryTransactionManager = sentryTransactionManager;
        }

        public void Enter((string email, CancellationToken ct) payload)
        {
            currentState.Value = AuthenticationStatus.VerificationInProgress;

            // Anim-OUT non-interactable Login Screen
            viewInstance.LoginScreenSubView.SlideOut();

            // Anim-IN Verification Screen
            viewInstance.VerificationOTPContainer.SetActive(true);
            viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.IN);

            // Update description with user email
            viewInstance.DescriptionOTP.text = viewInstance.DescriptionOTP.text.Replace("your@email.com", payload.email);

            // Reset OTP result UI
            viewInstance.OTPSubmitResultText.gameObject.SetActive(false);

            // Listeners
            viewInstance.CancelAuthenticationProcessOTP.onClick.AddListener(controller.CancelLoginProcess);
            viewInstance.ResendOTPButton.onClick.AddListener(ResendOtp);
            viewInstance.OTPInputField.CodeEntered += OnOTPEntered;

            AuthenticateAsync(payload.email, payload.ct).Forget();
        }

        public override void Exit()
        {
            viewInstance.OTPInputField.Clear();

            viewInstance.CancelAuthenticationProcessOTP.onClick.RemoveListener(controller.CancelLoginProcess);
            viewInstance.ResendOTPButton.onClick.RemoveListener(ResendOtp);
            viewInstance.OTPInputField.CodeEntered -= OnOTPEntered;
        }

        private void ResendOtp()
        {
            ResendOtpAsync().Forget();
            return;

            async UniTaskVoid ResendOtpAsync()
            {
                viewInstance.ResendOTPButton.interactable = false;

                try
                {
                    await web3Authenticator.ResendOtp();
                    viewInstance.OTPInputField.Clear();
                }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION)); }
                finally { viewInstance.ResendOTPButton.interactable = true; }
            }
        }

        private async UniTaskVoid AuthenticateAsync(string email, CancellationToken ct)
        {
            try
            {
                controller.CurrentRequestID = string.Empty;

                var web3AuthSpan = new SpanData
                {
                    TransactionName = LOADING_TRANSACTION_NAME,
                    SpanName = "Web3Authentication",
                    SpanOperation = "auth.web3_login",
                    Depth = 1,
                };

                sentryTransactionManager.StartSpan(web3AuthSpan);

                // awaits OTP code being entered
                IWeb3Identity identity = await web3Authenticator.LoginPayloadedAsync(LoginMethod.EMAIL_OTP, email, ct);

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.OUT);
                machine.Enter<ProfileFetchingOTPAuthState, (string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>((email, identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.BACK);
                machine.Enter<LoginStartAuthState>();
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.BACK);
                machine.Enter<LoginStartAuthState>();
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.BACK);
                machine.Enter<LoginStartAuthState>();
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.BACK);
                machine.Enter<LoginStartAuthState>();
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.BACK);
                machine.Enter<LoginStartAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
        }

        private void OnOTPEntered(string otp)
        {
            viewInstance.OTPSubmitResultText.gameObject.SetActive(false);
            viewInstance.OTPSubmitResultSucessIcon.SetActive(false);
            viewInstance.OTPSubmitResultErrorIcon.SetActive(false);

            OnOtpEnteredAsync().Forget();
            return;

            async UniTask OnOtpEnteredAsync()
            {
                try
                {
                    await web3Authenticator.SubmitOtp(otp);
                    ShowOtpSuccess();
                }
                catch (CodeVerificationException)
                {
                    ShowOtpError();
                }
            }
        }

        private void ShowOtpSuccess()
        {
            viewInstance.OTPInputField.SetSuccess();
            viewInstance.OTPSubmitResultText.gameObject.SetActive(true);
            viewInstance.OTPSubmitResultText.text = "Success";
            viewInstance.OTPSubmitResultSucessIcon.SetActive(true);
            viewInstance.OTPSubmitResultErrorIcon.SetActive(false);
        }

        private void ShowOtpError()
        {
            viewInstance.OTPInputField.SetFailure();
            viewInstance.ShakeOtpInputField();

            viewInstance.OTPSubmitResultText.gameObject.SetActive(true);
            viewInstance.OTPSubmitResultText.text = "Incorrect code";
            viewInstance.OTPSubmitResultSucessIcon.SetActive(false);
            viewInstance.OTPSubmitResultErrorIcon.SetActive(true);
        }
    }
}
