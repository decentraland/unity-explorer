using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics;
using DCL.Settings.Utils;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Global.AppArgs;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow
{
    public class IdentityVerificationDappAuthState : AuthStateBase, IPayloadedState<(LoginMethod method, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly VerificationDappAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly IAppArgs appArgs;
        private readonly List<Resolution> possibleResolutions;

        private Exception? loginException;

        public IdentityVerificationDappAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider,
            IAppArgs appArgs,
            List<Resolution> possibleResolutions) : base(viewInstance)
        {
            this.machine = machine;
            view = viewInstance.VerificationDappAuthView;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.appArgs = appArgs;
            this.possibleResolutions = possibleResolutions;
        }

        public void Enter((LoginMethod method, CancellationToken ct) payload)
        {
            base.Enter();

            loginException = null;

            // Checks the current screen mode because it could have been overridden with Alt+Enter
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();

            controller.CurrentRequestID = string.Empty;

            AuthenticateAsync(payload.method, payload.ct).Forget();
        }

        public override void Exit()
        {
            if (loginException == null)
                view.Hide(OUT);
            else
            {
                if (currentState.Value == AuthStatus.VerificationRequested)
                    view.Hide(SLIDE);

                spanErrorInfo = loginException switch
                {
                    OperationCanceledException => new SpanErrorInfo("Login process was cancelled by user"),
                    SignatureExpiredException ex => new SpanErrorInfo("Web3 signature expired during authentication", ex),
                    Web3SignatureException ex => new SpanErrorInfo("Web3 signature validation failed", ex),
                    CodeVerificationException ex => new SpanErrorInfo("Code verification failed during authentication", ex),
                    Web3Exception ex => new SpanErrorInfo("Connection error during authentication flow", ex),
                    Exception ex => new SpanErrorInfo("Unexpected error during authentication flow", ex),
                };

                if (loginException is not OperationCanceledException)
                    ReportHub.LogException(loginException, new ReportData(ReportCategory.AUTHENTICATION));
            }

            RestoreResolutionAndScreenMode();
            view.BackButton.onClick.RemoveListener(controller.CancelLoginProcess);
            base.Exit();
        }

        private async UniTaskVoid AuthenticateAsync(LoginMethod method, CancellationToken ct)
        {
            try
            {
                compositeWeb3Provider.VerificationRequired += ShowVerification;
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForDappFlow(method), ct);
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
            catch (CodeVerificationException e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Web3Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
            catch (Exception e)
            {
                loginException = e;
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
            finally
            {
                compositeWeb3Provider.VerificationRequired -= ShowVerification;
            }
        }

        private void ShowVerification((int code, DateTime expiration, string requestId) data)
        {
            compositeWeb3Provider.VerificationRequired -= ShowVerification;

            controller.CurrentRequestID = data.requestId;
            currentState.Value = AuthStatus.VerificationRequested;

            SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, new SpanData
            {
                SpanName = "CodeVerification",
                SpanOperation = "auth.code_verification",
                Depth = STATE_SPAN_DEPTH + 1,
            });

            // Hide non-interactable Login Screen
            viewInstance.LoginSelectionAuthView.Hide();

            // Show Verification Screen
            view.Show(data.code, data.expiration);
            view.BackButton.onClick.AddListener(controller.CancelLoginProcess);
        }

        private void RestoreResolutionAndScreenMode()
        {
            Resolution targetResolution = WindowModeUtils.GetTargetResolution(possibleResolutions);
            FullScreenMode targetScreenMode = WindowModeUtils.GetTargetScreenMode(appArgs.HasFlag(AppArgsFlags.WINDOWED_MODE));
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }
    }
}
