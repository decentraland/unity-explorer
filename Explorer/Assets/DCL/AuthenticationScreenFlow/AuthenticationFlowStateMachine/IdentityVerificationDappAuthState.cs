using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics;
using DCL.Settings.Utils;
using DCL.Utilities;
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

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class IdentityVerificationDappAuthState : AuthStateBase, IPayloadedState<(LoginMethod method, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly VerificationDappAuthView view;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IAppArgs appArgs;
        private readonly List<Resolution> possibleResolutions;
        private readonly SentryTransactionManager sentryTransactionManager;

        public IdentityVerificationDappAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IAppArgs appArgs,
            List<Resolution> possibleResolutions,
            SentryTransactionManager sentryTransactionManager) : base(viewInstance)
        {
            this.machine = machine;
            view = viewInstance.VerificationDappAuthView;
            this.controller = controller;
            this.currentState = currentState;
            this.web3Authenticator = web3Authenticator;
            this.appArgs = appArgs;
            this.possibleResolutions = possibleResolutions;
            this.sentryTransactionManager = sentryTransactionManager;
        }

        public void Enter((LoginMethod method, CancellationToken ct) payload)
        {
            // Checks the current screen mode because it could have been overridden with Alt+Enter
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();

            AuthenticateAsync(payload.method, payload.ct).Forget();
        }

        public override void Exit()
        {
            RestoreResolutionAndScreenMode();
            view.BackButton.onClick.RemoveListener(controller.CancelLoginProcess);
        }

        private async UniTaskVoid AuthenticateAsync(LoginMethod method, CancellationToken ct)
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

                web3Authenticator.VerificationRequired += ShowVerification;
                IWeb3Identity identity = await web3Authenticator.LoginAsync(method, ct);

                view.Hide(OUT);
                machine.Enter<ProfileFetchingAuthState, (IWeb3Identity identity, bool isCached, CancellationToken ct)>((identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");

                if (currentState.Value == AuthenticationStatus.VerificationInProgress)
                    view.Hide(BACK);

                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, BACK));
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthenticationStatus.VerificationInProgress)
                    view.Hide(BACK);

                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, BACK));
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthenticationStatus.VerificationInProgress)
                    view.Hide(BACK);

                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, BACK));
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthenticationStatus.VerificationInProgress)
                    view.Hide(BACK);

                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, BACK));
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthenticationStatus.VerificationInProgress)
                    view.Hide(BACK);

                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.CONNECTION_ERROR, BACK));
            }
            finally
            {
               web3Authenticator.VerificationRequired -= ShowVerification;
            }
        }

        private void ShowVerification((int code, DateTime expiration, string requestId) data)
        {
            web3Authenticator.VerificationRequired -= ShowVerification;
            currentState.Value = AuthenticationStatus.VerificationInProgress;

            controller.CurrentRequestID = data.requestId;

            var verificationSpan = new SpanData
            {
                TransactionName = LOADING_TRANSACTION_NAME,
                SpanName = "CodeVerification",
                SpanOperation = "auth.code_verification",
                Depth = 1,
            };

            sentryTransactionManager.StartSpan(verificationSpan);

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
