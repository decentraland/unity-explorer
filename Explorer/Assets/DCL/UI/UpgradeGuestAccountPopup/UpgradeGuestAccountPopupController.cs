using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Authenticators;
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

        private readonly IAccountLinkAuthenticator accountLinkAuthenticator;
        private readonly ISelfProfile selfProfile;

        private UniTaskCompletionSource lifeCycleTask = new ();
        private CancellationTokenSource linkCts = new ();

        public event Action<GuestUpgradeTrigger>? PromptShown;
        public event Action<GuestUpgradeTrigger>? UpgradeStarted;
        public event Action<GuestUpgradeTrigger>? UpgradeCompleted;
        public event Action<GuestUpgradeTrigger, string>? UpgradeFailed;

        public UpgradeGuestAccountPopupController(
            ViewFactoryMethod viewFactory,
            IAccountLinkAuthenticator accountLinkAuthenticator,
            ISelfProfile selfProfile) : base(viewFactory)
        {
            this.accountLinkAuthenticator = accountLinkAuthenticator;
            this.selfProfile = selfProfile;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.CloseButton.onClick.AddListener(Close);
            viewInstance.RegisterEMailCloseButton.onClick.AddListener(Close);
            viewInstance.VerifyOTPCloseButton.onClick.AddListener(Close);
            viewInstance.ConfirmSuccessButton.onClick.AddListener(Close);

            viewInstance.UpgradeAccountButton.onClick.AddListener(() => ShowStep(Step.RegisterEmail));
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

        protected override void OnViewClose()
        {
            base.OnViewClose();
            linkCts = linkCts.SafeRestart();
        }

        public override void Dispose()
        {
            base.Dispose();
            linkCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.EMailInputField.Submitted -= OnEmailSubmitted;
            viewInstance.OTPInputField.CodeEntered -= OnOTPEntered;
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
                await accountLinkAuthenticator.SendEmailLinkOtpAsync(email, ct);

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
                await accountLinkAuthenticator.LinkEmailAsync(otp, ct);

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
                await accountLinkAuthenticator.ResendEmailLinkOtpAsync(ct);

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
