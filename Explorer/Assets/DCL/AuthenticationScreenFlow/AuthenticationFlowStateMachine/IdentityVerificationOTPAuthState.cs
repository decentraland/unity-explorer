using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class IdentityVerificationOTPAuthState : AuthStateBase, IPayloadedState<(string email, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly VerificationOTPAuthView view;

        public IdentityVerificationOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            IWeb3VerifiedAuthenticator web3Authenticator,
            SentryTransactionManager sentryTransactionManager) : base(viewInstance)
        {
            view = viewInstance.VerificationOTPAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.web3Authenticator = web3Authenticator;
            this.sentryTransactionManager = sentryTransactionManager;
        }

        public void Enter((string email, CancellationToken ct) payload)
        {
            currentState.Value = AuthenticationStatus.VerificationInProgress;

            view.Show(payload.email);
            AuthenticateAsync(payload.email, payload.ct).Forget();

            // Listeners
            view.BackButton.onClick.AddListener(controller.CancelLoginProcess);
            view.ResendCodeButton.onClick.AddListener(ResendOtp);
            view.InputField.CodeEntered += OnEntered;
        }

        public override void Exit()
        {
            view.InputField.Clear();

            // Listeners
            view.BackButton.onClick.RemoveAllListeners();
            view.ResendCodeButton.onClick.RemoveAllListeners();
            view.InputField.CodeEntered -= OnEntered;
        }

        private void ResendOtp()
        {
            ResendOtpAsync().Forget();
            return;

            async UniTaskVoid ResendOtpAsync()
            {
                view.ResendCodeButton.interactable = false;

                try
                {
                    await web3Authenticator.ResendOtp();
                    view.InputField.Clear();
                }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION)); }
                finally { view.ResendCodeButton.interactable = true; }
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

                view.Hide(OUT);
                machine.Enter<ProfileFetchingOTPAuthState, (string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>((email, identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.CONNECTION_ERROR, SLIDE));
            }
        }

        private void OnEntered(string otp)
        {
            view.VerificationResultText.gameObject.SetActive(false);
            view.VerificationSuccessIcon.SetActive(false);
            view.VerificationErrorIcon.SetActive(false);

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
            view.InputField.SetSuccess();

            view.VerificationResultText.gameObject.SetActive(true);
            view.VerificationResultText.text = "Success";
            view.VerificationSuccessIcon.SetActive(true);
            view.VerificationErrorIcon.SetActive(false);
        }

        private void ShowOtpError()
        {
            view.InputField.SetFailure();

            view.VerificationResultText.gameObject.SetActive(true);
            view.VerificationResultText.text = "Incorrect code";
            view.VerificationSuccessIcon.SetActive(false);
            view.VerificationErrorIcon.SetActive(true);

            // After showing error feedback, clear the field and refocus it so user can retry
            view.InputField.ClearAndFocusDelayed(1.5f);
        }
    }
}
