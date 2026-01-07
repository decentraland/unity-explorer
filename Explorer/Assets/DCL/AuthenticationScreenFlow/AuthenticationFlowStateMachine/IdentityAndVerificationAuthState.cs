using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics;
using DCL.Settings.Utils;
using DCL.UI;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Global.AppArgs;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class IdentityAndVerificationAuthState : AuthStateBase, IPayloadedState<CancellationToken>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly IAppArgs appArgs;
        private readonly List<Resolution> possibleResolutions;
        private readonly SentryTransactionManager sentryTransactionManager;
        private CancellationTokenSource? verificationCountdownCancellationToken;

        public IdentityAndVerificationAuthState(
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
            this.controller = controller;
            this.currentState = currentState;
            this.web3Authenticator = web3Authenticator;
            this.appArgs = appArgs;
            this.possibleResolutions = possibleResolutions;
            this.sentryTransactionManager = sentryTransactionManager;
        }

        public void Enter(CancellationToken ct)
        {
            // Checks the current screen mode because it could have been overridden with Alt+Enter
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();

            AuthenticateAsync(ct).Forget();
        }

        public override void Exit()
        {
            RestoreResolutionAndScreenMode();

            CancelVerificationCountdown();

            viewInstance.VerificationCodeHintContainer.SetActive(false);
            viewInstance.VerificationContainer.SetActive(false);

            viewInstance.LoginContainer.SetActive(false);

            // Listeners
            viewInstance.CancelAuthenticationProcess.onClick.RemoveListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.RemoveListener(ToggleVerificationCodeVisibility);
        }

        private async UniTaskVoid AuthenticateAsync(CancellationToken ct)
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
                IWeb3Identity identity = await web3Authenticator.LoginAsync(ct);

                machine.Enter<ProfileFetchingAuthState, (IWeb3Identity identity, bool isCached, CancellationToken ct)>((identity, false, ct));
            }
            catch (OperationCanceledException)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                machine.Enter<LoginStartAuthState>();
            }
            catch (SignatureExpiredException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature expired during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                machine.Enter<LoginStartAuthState>();
            }
            catch (Web3SignatureException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Web3 signature validation failed", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                machine.Enter<LoginStartAuthState>();
            }
            catch (CodeVerificationException e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Code verification failed during authentication", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                machine.Enter<LoginStartAuthState>();
            }
            catch (Exception e)
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Unexpected error during authentication flow", e);
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                machine.Enter<LoginStartAuthState, PopupType>(PopupType.CONNECTION_ERROR);
            }
            finally
            {
               web3Authenticator.VerificationRequired -= ShowVerification;
            }
        }

        private void RestoreResolutionAndScreenMode()
        {
            Resolution targetResolution = WindowModeUtils.GetTargetResolution(possibleResolutions);
            FullScreenMode targetScreenMode = WindowModeUtils.GetTargetScreenMode(appArgs.HasFlag(AppArgsFlags.WINDOWED_MODE));
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }

        private void ShowVerification((int code, DateTime expiration, string requestId) data)
        {
            web3Authenticator.VerificationRequired -= ShowVerification;
            currentState.Value = AuthenticationStatus.VerificationInProgress;

            viewInstance!.VerificationCodeLabel.text = data.code.ToString();
            controller.CurrentRequestID = data.requestId;

            var verificationSpan = new SpanData
            {
                TransactionName = LOADING_TRANSACTION_NAME,
                SpanName = "CodeVerification",
                SpanOperation = "auth.code_verification",
                Depth = 1,
            };

            sentryTransactionManager.StartSpan(verificationSpan);

            CancelVerificationCountdown();
            verificationCountdownCancellationToken = new CancellationTokenSource();

            viewInstance.StartVerificationCountdownAsync(data.expiration, verificationCountdownCancellationToken.Token)
                        .Forget();

            // Anim-OUT non-interactable Login Screen
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.OUT);

            viewInstance.LoginButton.gameObject.SetActive(true);
            viewInstance.LoginButton.interactable = false;

            viewInstance.LoadingSpinner.SetActive(false);

            // Anim-IN Verification Screen
            viewInstance.VerificationContainer.SetActive(true);
            viewInstance.VerificationAnimator.ResetAnimator();
            viewInstance.VerificationAnimator.SetTrigger(UIAnimationHashes.IN);

            // Listeners
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(controller.CancelLoginProcess);
            viewInstance.VerificationCodeHintButton.onClick.AddListener(ToggleVerificationCodeVisibility);
        }

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private void ToggleVerificationCodeVisibility() =>
            viewInstance!.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
    }
}
