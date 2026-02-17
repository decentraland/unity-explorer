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
        private readonly VerificationOTPAuthView view;

        public event Action<string, bool>? OTPVerified;
        public event Action? OTPResend;

        private string email;
        private CancellationToken loginCt;

        private Exception? loginException;

        public IdentityVerificationOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            view = viewInstance.VerificationOTPAuthView;

            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;

            email = string.Empty;
            loginCt = CancellationToken.None;
        }

        public void Enter((string email, CancellationToken ct) payload)
        {
            base.Enter();
            loginException = null;

            currentState.Value = AuthStatus.VerificationRequested;
            email = payload.email;
            loginCt = payload.ct;

            view.Show(payload.email);
            AuthenticateAsync(payload.email, payload.ct).Forget();

            // Listeners
            view.BackButton.onClick.AddListener(controller.CancelLoginProcess);
            view.ResendCodeButton.onClick.AddListener(ResendOtp);
            view.InputField.CodeEntered += OnOTPEntered;
        }

        public override void Exit()
        {
            if (loginException == null)
                view.Hide(OUT);
            else
            {
                view.Hide(SLIDE);

                spanErrorInfo = loginException switch
                {
                    OperationCanceledException => new SpanErrorInfo("Login process was cancelled by user"),
                    SignatureExpiredException ex => new SpanErrorInfo("Web3 signature expired during authentication", ex),
                    Web3SignatureException ex => new SpanErrorInfo("Web3 signature validation failed", ex),
                    CodeVerificationException ex => new SpanErrorInfo("Code verification failed during authentication", ex),
                    Exception ex => new SpanErrorInfo("Unexpected error during authentication flow", ex),
                };

                if (loginException is not OperationCanceledException)
                    ReportHub.LogException(loginException, new ReportData(ReportCategory.AUTHENTICATION));
            }

            // Listeners
            view.BackButton.onClick.RemoveAllListeners();
            view.ResendCodeButton.onClick.RemoveAllListeners();
            view.InputField.CodeEntered -= OnOTPEntered;

            email = string.Empty;
            loginCt = CancellationToken.None;
            base.Exit();
        }

        private async UniTaskVoid AuthenticateAsync(string email, CancellationToken ct)
        {
            try
            {
                controller.CurrentRequestID = string.Empty;

                // awaits OTP code being entered
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForOtpFlow(email), ct);
                machine.Enter<ProfileFetchingAuthState, ProfileFetchingPayload>(new (email, identity, false, ct));
            }
            catch (OperationCanceledException e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (SignatureExpiredException e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Web3SignatureException e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (CodeVerificationException e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
        }

        private void OnOTPEntered(string otp)
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
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    controller.CancelLoginProcess();
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
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    controller.CancelLoginProcess();
                }
                finally { view.ResendCodeButton.interactable = true; }
            }
        }
    }
}
