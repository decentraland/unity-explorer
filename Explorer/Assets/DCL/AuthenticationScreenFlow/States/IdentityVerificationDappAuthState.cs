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

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
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
        private readonly SentryTransactionManager sentryTransactionManager;

        public IdentityVerificationDappAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ICompositeWeb3Provider compositeWeb3Provider,
            IAppArgs appArgs,
            List<Resolution> possibleResolutions,
            SentryTransactionManager sentryTransactionManager) : base(viewInstance)
        {
            this.machine = machine;
            view = viewInstance.VerificationDappAuthView;
            this.controller = controller;
            this.currentState = currentState;
            this.compositeWeb3Provider = compositeWeb3Provider;
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

                compositeWeb3Provider.VerificationRequired += ShowVerification;
                IWeb3Identity identity = await compositeWeb3Provider.LoginAsync(LoginPayload.ForDappFlow(method), ct);

                view.Hide(OUT);
                machine.Enter<ProfileFetchingAuthState, (IWeb3Identity identity, bool isCached, CancellationToken ct)>((identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

                machine.Enter<LoginSelectionAuthState, int>(SLIDE);
            }
            catch (Web3Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Connection  error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                if (currentState.Value == AuthStatus.VerificationInProgress)
                    view.Hide(SLIDE);

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
            currentState.Value = AuthStatus.VerificationInProgress;

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
