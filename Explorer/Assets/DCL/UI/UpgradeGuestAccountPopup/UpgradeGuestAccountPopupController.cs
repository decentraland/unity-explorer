using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.UpgradeGuestAccountPopup
{
    public class UpgradeGuestAccountPopupController : ControllerBase<UpgradeGuestAccountPopupView, UpgradeGuestAccountPopupController.Params>
    {
        public readonly struct Params
        {
            public readonly GuestUpgradeTrigger Trigger;

            public Params(GuestUpgradeTrigger trigger)
            {
                Trigger = trigger;
            }
        }

        private enum Step
        {
            RestrictedUser,
            RegisterEmail,
            VerifyOTP,
            VerifyEmail,
            Success,
            EmailAlreadyExist,
            Error,
        }

        private readonly ICompositeWeb3Provider compositeWeb3Provider;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileCache profileCache;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly World world;
        private readonly Entity playerEntity;

        private UniTaskCompletionSource lifeCycleTask = new ();
        private CancellationTokenSource linkCts = new ();
        private CancellationTokenSource logoutCts = new ();

        public event Action<GuestUpgradeTrigger>? PromptShown;
        public event Action<GuestUpgradeTrigger>? UpgradeStarted;
        public event Action<GuestUpgradeTrigger>? UpgradeRedirectedToAccountCreation;
        public event Action<GuestUpgradeTrigger>? UpgradeCompleted;
        public event Action<GuestUpgradeTrigger, string>? UpgradeFailed;

        public UpgradeGuestAccountPopupController(
            ViewFactoryMethod viewFactory,
            ICompositeWeb3Provider compositeWeb3Provider,
            ISelfProfile selfProfile,
            IInputBlock inputBlock,
            IWeb3IdentityCache identityCache,
            IProfileCache profileCache,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            World world,
            Entity playerEntity) : base(viewFactory)
        {
            this.compositeWeb3Provider = compositeWeb3Provider;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;
            this.identityCache = identityCache;
            this.profileCache = profileCache;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.CloseButton.onClick.AddListener(Close);
            viewInstance.RegisterEMailCloseButton.onClick.AddListener(Close);
            viewInstance.VerifyOTPCloseButton.onClick.AddListener(Close);
            viewInstance.ConfirmSuccessButton.onClick.AddListener(Close);

            viewInstance.UpgradeAccountButton.onClick.AddListener(UpgradeAccount);
            viewInstance.VerifyOTPBackButton.onClick.AddListener(() => ShowStep(Step.RegisterEmail));
            viewInstance.TryAnotherEmailButton.onClick.AddListener(() => ShowStep(Step.RegisterEmail));
            viewInstance.ErrorRetryButton.onClick.AddListener(() => ShowStep(Step.RegisterEmail));
            viewInstance.VerifyEmailCancelButton.onClick.AddListener(CancelEmailVerification);

            viewInstance.EMailInputField.Submitted += OnEmailSubmitted;
            viewInstance.OTPInputField.CodeEntered += OnOTPEntered;
            viewInstance.ResendOTPButton.onClick.AddListener(ResendOTP);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            ShowStep(Step.RestrictedUser);
            PromptShown?.Invoke(inputData.Trigger);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
            linkCts = linkCts.SafeRestart();
        }

        public override void Dispose()
        {
            base.Dispose();
            linkCts.SafeCancelAndDispose();
            logoutCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.EMailInputField.Submitted -= OnEmailSubmitted;
            viewInstance.OTPInputField.CodeEntered -= OnOTPEntered;
        }

        private void UpgradeAccount()
        {
            // An ephemeral account holds no wallet to link an email to, so the only way forward is a real login
            if (identityCache.Identity?.Method == LoginMethod.EPHEMERAL_GUEST)
            {
                UpgradeRedirectedToAccountCreation?.Invoke(inputData.Trigger);
                logoutCts = logoutCts.SafeRestart();
                LogoutAndShowLoginSelectionAsync(logoutCts.Token).Forget();
                return;
            }

            ShowStep(Step.RegisterEmail);
        }

        private async UniTaskVoid LogoutAndShowLoginSelectionAsync(CancellationToken ct)
        {
            try
            {
                if (identityCache.Identity == null)
                {
                    ReportHub.LogError(ReportCategory.UI, "Cannot logout. Identity is null.");
                    return;
                }

                Web3Address address = identityCache.Identity.Address;
                await compositeWeb3Provider.LogoutAsync(ct);
                profileCache.Remove(address);
                Close();

                await userInAppInitializationFlow.ExecuteAsync(
                    new UserInAppInitializationFlowParameters(
                        showAuthentication: true,
                        showLoading: true,
                        loadSource: IUserInAppInitializationFlow.LoadSource.Logout,
                        world: world,
                        playerEntity: playerEntity,
                        startAtLoginSelection: true
                    ),
                    ct
                );
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private void ShowStep(Step step)
        {
            viewInstance!.RestrictedUserRoot.SetActive(step == Step.RestrictedUser);
            viewInstance.RegisterEMailRoot.SetActive(step == Step.RegisterEmail);
            viewInstance.VerifyOTPRoot.SetActive(step == Step.VerifyOTP);
            viewInstance.VerifyEmailRoot.SetActive(step == Step.VerifyEmail);
            viewInstance.SuccessRoot.SetActive(step == Step.Success);
            viewInstance.EMailFailedRoot.SetActive(step == Step.EmailAlreadyExist);
            viewInstance.ErrorRoot.SetActive(step == Step.Error);
        }

        private void ShowInvalidEmail()
        {
            ShowStep(Step.RegisterEmail);
            viewInstance!.EMailInputField.SetErrorState(true);
        }

        private void Close() =>
            lifeCycleTask.TrySetResult();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask = new UniTaskCompletionSource();
            return lifeCycleTask.Task.AttachExternalCancellation(ct);
        }

        private void OnEmailSubmitted()
        {
            ShowStep(Step.VerifyEmail);
            UpgradeStarted?.Invoke(inputData.Trigger);
            SendLinkOtpAsync(viewInstance!.EMailInputField.Text, linkCts.Token).Forget();
        }

        private void CancelEmailVerification()
        {
            linkCts = linkCts.SafeRestart();
            ShowStep(Step.RegisterEmail);
        }

        private async UniTaskVoid SendLinkOtpAsync(string email, CancellationToken ct)
        {
            try
            {
                await compositeWeb3Provider.SendEmailLinkOtpAsync(email, ct);

                if (ct.IsCancellationRequested) return;

                ShowStep(Step.VerifyOTP);
            }
            catch (OperationCanceledException) { }
            catch (InvalidEmailException)
            {
                if (ct.IsCancellationRequested) return;

                UpgradeFailed?.Invoke(inputData.Trigger, "invalid_email");
                ShowInvalidEmail();
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested) return;

                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                UpgradeFailed?.Invoke(inputData.Trigger, "otp_send_error");
                ShowStep(Step.Error);
            }
        }

        private void OnOTPEntered(string otp) =>
            LinkEmailAsync(otp, linkCts.Token).Forget();

        private async UniTaskVoid LinkEmailAsync(string otp, CancellationToken ct)
        {
            try
            {
                await compositeWeb3Provider.LinkEmailAsync(otp, ct);

                if (ct.IsCancellationRequested) return;

                viewInstance!.OTPInputField.SetSuccess();
                await PromoteProfileAsync(ct);

                if (ct.IsCancellationRequested) return;

                UpgradeCompleted?.Invoke(inputData.Trigger);
                ShowStep(Step.Success);
            }
            catch (OperationCanceledException) { }
            catch (CodeVerificationException)
            {
                if (ct.IsCancellationRequested) return;

                UpgradeFailed?.Invoke(inputData.Trigger, "invalid_code");
                viewInstance!.OTPInputField.SetFailure();
            }
            catch (EmailAlreadyLinkedException)
            {
                if (ct.IsCancellationRequested) return;

                UpgradeFailed?.Invoke(inputData.Trigger, "email_already_linked");
                ShowStep(Step.EmailAlreadyExist);
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested) return;

                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                UpgradeFailed?.Invoke(inputData.Trigger, "link_error");
                ShowStep(Step.Error);
            }
        }

        private async UniTask PromoteProfileAsync(CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null || profile.HasConnectedWeb3) return;

            Profile promotedProfile = new ProfileBuilder().From(profile)
                                                          .WithGuestMode(false)
                                                          .Build();

            await selfProfile.UpdateProfileAsync(promotedProfile, ct);
        }

        private void ResendOTP() =>
            ResendOTPAsync(linkCts.Token).Forget();

        private async UniTaskVoid ResendOTPAsync(CancellationToken ct)
        {
            viewInstance!.ResendOTPButton.interactable = false;

            try
            {
                await compositeWeb3Provider.ResendEmailLinkOtpAsync(ct);

                if (ct.IsCancellationRequested) return;

                viewInstance.OTPInputField.Clear();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION)); }
            finally
            {
                if (viewInstance != null)
                    viewInstance.ResendOTPButton.interactable = true;
            }
        }
    }
}
