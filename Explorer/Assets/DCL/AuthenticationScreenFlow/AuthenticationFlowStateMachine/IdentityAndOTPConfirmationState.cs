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
using Utility;
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

            // Anim-IN Verification Screen
            viewInstance.VerificationOTPContainer.SetActive(true);
            viewInstance.VerificationOTPAnimator.ResetAnimator();
            viewInstance.VerificationOTPAnimator.SetTrigger(UIAnimationHashes.IN);

            // Listeners
            viewInstance.CancelAuthenticationProcessOTP.onClick.AddListener(CancelLoginProcess);
            viewInstance.OTPInputField.OnCodeComplete += OnOtpEntered;

            AuthenticateAsync(payload.email, payload.ct).Forget();
        }

        public override void Exit()
        {
            viewInstance.VerificationOTPContainer.SetActive(false);
            viewInstance.CancelAuthenticationProcessOTP.onClick.RemoveListener(CancelLoginProcess);
            viewInstance.OTPInputField.OnCodeComplete -= OnOtpEntered;
        }

        private void CancelLoginProcess()
        {
            controller.CancelLoginProcess();
            machine.Enter<LoginStartAuthState>();
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

                IWeb3Identity identity = await web3Authenticator.LoginAsync(email, ct);

                machine.Enter<ProfileFetchingOTPAuthState, (string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>((email, identity, false, ct));
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
        }

        private void OnOtpEntered(string otp) =>
            web3Authenticator.SubmitOtp(otp);

        //  async UniTaskVoid StartLoginFlowUntilEndAsync(CancellationToken ct)
        // {
        //     try
        //     {
        //         var identityValidationSpan = new SpanData
        //         {
        //             TransactionName = LOADING_TRANSACTION_NAME,
        //             SpanName = "IdentityValidation",
        //             SpanOperation = "auth.identity_validation",
        //             Depth = 1,
        //         };
        //
        //         sentryTransactionManager.StartSpan(identityValidationSpan);
        //
        //         if (IsUserAllowedToAccessToBeta(identity))
        //         {
        //             CurrentState.Value = AuthenticationStatus.FetchingProfile;
        //
        //             // SwitchState(ViewState.Loading);
        //
        //             var profileFetchSpan = new SpanData
        //             {
        //                 TransactionName = LOADING_TRANSACTION_NAME,
        //                 SpanName = "FetchProfile",
        //                 SpanOperation = "auth.profile_fetch",
        //                 Depth = 1,
        //             };
        //
        //             sentryTransactionManager.StartSpan(profileFetchSpan);
        //
        //             var walletAddress = identity.Address.ToString();
        //             bool profileExists = await CheckProfileExistsAsync(walletAddress, ct);
        //             bool isNewUser = !profileExists && ThirdWebManager.Instance.ActiveWallet != null;
        //
        //             if (isNewUser)
        //             {
        //                 IWeb3Identity? identity1 = storedIdentityProvider.Identity;
        //
        //                 if (identity1 == null)
        //                     throw new Web3IdentityMissingException("Web3 identity is not available when creating a default profile");
        //
        //                 // Load base wearables catalog for randomization
        //                 await LoadBaseWearablesAsync(ct);
        //
        //                 newUserProfile = BuildDefaultProfile(identity1.Address.ToString(), currentEmail);
        //                 newUserProfile.HasConnectedWeb3 = true;
        //
        //                 characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
        //                 InitializeAvatarHistory(newUserProfile.Avatar);
        //                 sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
        //
        //                 CurrentState.Value = AuthenticationStatus.LoggedIn;
        //                 SwitchState(ViewState.FinalizeNewUser);
        //             }
        //             else
        //             {
        //                 Profile? profile = await selfProfile.ProfileAsync(ct);
        //
        //                 profile!.IsDirty = true;
        //                 profile.HasConnectedWeb3 = true;
        //
        //                 profileNameLabel!.Value = profile.Version == 1 ? profile.Name : "back " + profile.Name;
        //                 characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
        //
        //                 characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
        //                 sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);
        //
        //                 CurrentState.Value = AuthenticationStatus.LoggedIn;
        //                 SwitchState(ViewState.Finalize);
        //             }
        //         }
        //         else
        //         {
        //             sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "User not allowed to access beta - restricted user (main)");
        //             SwitchState(ViewState.Login);
        //             ShowRestrictedUserPopup();
        //         }
        //     }
        // }
    }
}
