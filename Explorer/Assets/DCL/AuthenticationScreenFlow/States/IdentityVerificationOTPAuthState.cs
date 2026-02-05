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

namespace DCL.AuthenticationScreenFlow
{
    public class IdentityVerificationOTPAuthState : AuthStateBase, IPayloadedState<(string email, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly VerificationOTPAuthView view;

        public event Action<string, bool>? OTPVerified;
        public event Action? OTPResend;

        private string email;
        private CancellationToken loginCt;

        public IdentityVerificationOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider,
            SentryTransactionManager sentryTransactionManager) : base(viewInstance)
        {
            view = viewInstance.VerificationOTPAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.sentryTransactionManager = sentryTransactionManager;

            email = string.Empty;
            loginCt = CancellationToken.None;
        }

        public void Enter((string email, CancellationToken ct) payload)
        {
            currentState.Value = AuthStatus.VerificationInProgress;
            email = payload.email;
            loginCt = payload.ct;

            view.Show(payload.email);
            AuthenticateAsync(payload.email, payload.ct).Forget();

            // Listeners
            view.BackButton.onClick.AddListener(controller.CancelLoginProcess);
            view.ResendCodeButton.onClick.AddListener(ResendOtp);
            view.InputField.CodeEntered += OnEntered;
        }

        public override void Exit()
        {
            // Listeners
            view.BackButton.onClick.RemoveAllListeners();
            view.ResendCodeButton.onClick.RemoveAllListeners();
            view.InputField.CodeEntered -= OnEntered;

            email = string.Empty;
            loginCt = CancellationToken.None;
        }

        private void ResendOtp()
        {
            ResendOtpAsync(loginCt).Forget();
            return;

            async UniTaskVoid ResendOtpAsync(CancellationToken ct)
            {
                view.ResendCodeButton.interactable = false;

                try
                {
                    await compositeWeb3Provider.ResendOtpAsync(ct);
                    OTPResend?.Invoke();
                    view.InputField.Clear();
                }
                catch (OperationCanceledException)
                { /* Expected on cancellation */
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
                }
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
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForOtpFlow(email), ct);

                // Close Web3Authentication span before transitioning to profile fetching
                sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                view.Hide(OUT);
                machine.Enter<ProfileFetchingOTPAuthState, (string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>((email, identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
        }

        private void OnEntered(string otp)
        {
            OnOtpEnteredAsync(loginCt).Forget();
            return;

            async UniTask OnOtpEnteredAsync(CancellationToken ct)
            {
                try
                {
                    await compositeWeb3Provider.SubmitOtpAsync(otp, ct);
                    ShowOtpResult(true);
                }
                catch (OperationCanceledException)
                { /* Expected on cancellation */
                }
                catch (CodeVerificationException)
                {
                    ShowOtpResult(false);
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
                }
            }
        }

        private void ShowOtpResult(bool isSuccess)
        {
            OTPVerified?.Invoke(email, isSuccess);

            if (isSuccess)
                view.InputField.SetSuccess();
            else
                view.InputField.SetFailure();
        }
    }
}
