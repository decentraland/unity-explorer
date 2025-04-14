using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles.Self;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsVerifyEmailController : IDisposable
    {
        private const int CHECKING_EMAIL_VERIFICATION_TIME_INTERVAL = 5;

        private readonly MarketplaceCreditsVerifyEmailView view;
        private readonly ISelfProfile selfProfile;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        private CancellationTokenSource checkEmailVerificationCts;
        private CancellationTokenSource updateEmailCts;
        private CancellationTokenSource resendVerificationEmailCts;
        private string currentEmail;

        public MarketplaceCreditsVerifyEmailController(
            MarketplaceCreditsVerifyEmailView view,
            ISelfProfile selfProfile,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;

            view.UpdateEmailButton.onClick.AddListener(UpdateEmail);
            view.ResendVerificationEmailButton.onClick.AddListener(ResendVerificationEmail);
        }

        public void OpenSection()
        {
            view.gameObject.SetActive(true);

            checkEmailVerificationCts = checkEmailVerificationCts.SafeRestart();
            CheckEmailVerificationAsync(checkEmailVerificationCts.Token).Forget();
        }

        public void CloseSection()
        {
            checkEmailVerificationCts.SafeCancelAndDispose();
            updateEmailCts.SafeCancelAndDispose();
            resendVerificationEmailCts.SafeCancelAndDispose();
            view.gameObject.SetActive(false);
        }

        public void Setup(string email)
        {
            view.SetEmailToVerify(email);
            currentEmail = email;
        }

        public void Dispose()
        {
            view.UpdateEmailButton.onClick.RemoveListener(UpdateEmail);
            view.ResendVerificationEmailButton.onClick.RemoveListener(ResendVerificationEmail);

            checkEmailVerificationCts.SafeCancelAndDispose();
            updateEmailCts.SafeCancelAndDispose();
            resendVerificationEmailCts.SafeCancelAndDispose();
        }

        private async UniTaskVoid CheckEmailVerificationAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(CHECKING_EMAIL_VERIFICATION_TIME_INTERVAL * 1000, cancellationToken: ct);

                try
                {
                    var ownProfile = await selfProfile.ProfileAsync(ct);
                    if (ownProfile != null)
                    {
                        var creditsProgramProgressResponse = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);
                        if (!creditsProgramProgressResponse.IsUserEmailVerified())
                            continue;

                        await marketplaceCreditsAPIClient.MarkUserAsStartedProgramAsync(ct);
                        marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.WELCOME);
                        break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    const string ERROR_MESSAGE = "There was an error checking the email verification. Please try again!";
                    marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                    ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
                }
            }
        }

        private void UpdateEmail()
        {
            updateEmailCts = updateEmailCts.SafeRestart();
            UpdateEmailAsync(updateEmailCts.Token).Forget();
        }

        private async UniTaskVoid UpdateEmailAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);

                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    // Removes email subscription
                    await marketplaceCreditsAPIClient.SubscribeEmailAsync(string.Empty, ct);
                    marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.WELCOME);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error removing your registration. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                view.SetAsLoading(false);
            }
        }

        private void ResendVerificationEmail()
        {
            resendVerificationEmailCts = resendVerificationEmailCts.SafeRestart();
            ResendVerificationEmailAsync(resendVerificationEmailCts.Token).Forget();
        }

        private async UniTaskVoid ResendVerificationEmailAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);

                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                    // Reset the email subscription
                    await marketplaceCreditsAPIClient.SubscribeEmailAsync(currentEmail, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error sending the verification email. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                view.SetAsLoading(false);
            }
        }
    }
}
