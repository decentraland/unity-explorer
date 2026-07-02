using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using Plugins.NativeWindowManager;
using System;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow
{
    public class IdentityVerificationDappDeepLinkAuthState : AuthStateBase, IPayloadedState<(LoginMethod method, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        private Exception? loginException;

        public IdentityVerificationDappDeepLinkAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider) : base(viewInstance)
        {
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;
        }

        public void Enter((LoginMethod method, CancellationToken ct) payload)
        {
            base.Enter();

            loginException = null;

            // Checks the current screen mode because it could have been overridden with Alt+Enter
            NativeWindowManager.RequestTemporaryWindowMode();

            controller.CurrentRequestID = string.Empty;

            currentState.Value = AuthStatus.VerificationRequested;
            AuthenticateAsync(payload.method, payload.ct).Forget();
        }

        public override void Exit()
        {
            if (loginException != null)
            {
                spanErrorInfo = loginException switch
                                {
                                    OperationCanceledException => new SpanErrorInfo("Login process was cancelled by user"),
                                    SignatureExpiredException ex => new SpanErrorInfo("Web3 signature expired during deep-link authentication", ex),
                                    Web3SignatureException ex => new SpanErrorInfo("Web3 signature validation failed during deep-link authentication", ex),
                                    Web3Exception ex => new SpanErrorInfo("Connection error during deep-link authentication flow", ex),
                                    Exception ex => new SpanErrorInfo("Unexpected error during deep-link authentication flow", ex),
                                };

                if (loginException is not OperationCanceledException)
                    ReportHub.LogException(loginException, new ReportData(ReportCategory.AUTHENTICATION));
            }

            NativeWindowManager.ReleaseTemporaryWindowMode();
            base.Exit();
        }

        private async UniTaskVoid AuthenticateAsync(LoginMethod method, CancellationToken ct)
        {
            try
            {
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForDappFlow(method), ct);
                viewInstance.LoginSelectionAuthView.Hide();
                machine.Enter<ProfileFetchingAuthState, ProfileFetchingPayload>(new (identity, false, ct));
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
            catch (Web3Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.CONNECTION_ERROR);
            }
            catch (Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.CONNECTION_ERROR);
            }
        }
    }
}
