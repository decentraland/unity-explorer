using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles.Self;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWelcomeSubController : IDisposable
    {
        private static readonly Regex EMAIL_PATTERN_REGEX = new (@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        private readonly MarketplaceCreditsWelcomeSubView subView;
        private readonly MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly MarketplaceCreditsVerifyEmailSubController marketplaceCreditsVerifyEmailSubController;
        private readonly MarketplaceCreditsGoalsOfTheWeekSubController marketplaceCreditsGoalsOfTheWeekSubController;
        private readonly MarketplaceCreditsWeekGoalsCompletedSubController marketplaceCreditsWeekGoalsCompletedSubController;
        private readonly MarketplaceCreditsProgramEndedSubController marketplaceCreditsProgramEndedSubController;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;

        private CreditsProgramProgressResponse currentCreditsProgramProgress;
        private CancellationTokenSource fetchProgramRegistrationInfoCts;
        private CancellationTokenSource registerInTheProgramCts;

        public MarketplaceCreditsWelcomeSubController(
            MarketplaceCreditsWelcomeSubView subView,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            MarketplaceCreditsVerifyEmailSubController marketplaceCreditsVerifyEmailSubController,
            MarketplaceCreditsGoalsOfTheWeekSubController marketplaceCreditsGoalsOfTheWeekSubController,
            MarketplaceCreditsWeekGoalsCompletedSubController marketplaceCreditsWeekGoalsCompletedSubController,
            MarketplaceCreditsProgramEndedSubController marketplaceCreditsProgramEndedSubController,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IInputBlock inputBlock)
        {
            this.subView = subView;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.marketplaceCreditsVerifyEmailSubController = marketplaceCreditsVerifyEmailSubController;
            this.marketplaceCreditsGoalsOfTheWeekSubController = marketplaceCreditsGoalsOfTheWeekSubController;
            this.marketplaceCreditsWeekGoalsCompletedSubController = marketplaceCreditsWeekGoalsCompletedSubController;
            this.marketplaceCreditsProgramEndedSubController = marketplaceCreditsProgramEndedSubController;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;

            subView.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            subView.StartWithEmailButton.onClick.AddListener(RegisterInTheProgramWithNewEmail);
            subView.StartButton.onClick.AddListener(RegisterInTheProgramWithExistingEmail);
            subView.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OpenSection()
        {
            subView.gameObject.SetActive(true);
            totalCreditsWidgetView.gameObject.SetActive(false);

            fetchProgramRegistrationInfoCts = fetchProgramRegistrationInfoCts.SafeRestart();
            LoadProgramRegistrationInfoAsync(fetchProgramRegistrationInfoCts.Token).Forget();

            subView.CleanEmailInput();
            OnEmailInputValueChanged(subView.EmailInput.text);
            CheckStartWithEmailButtonState();
        }

        public void CloseSection()
        {
            subView.gameObject.SetActive(false);
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void Dispose()
        {
            subView.LearnMoreLinkButton.onClick.RemoveListener(OpenLearnMoreLink);
            subView.StartWithEmailButton.onClick.RemoveListener(RegisterInTheProgramWithNewEmail);
            subView.StartButton.onClick.RemoveListener(RegisterInTheProgramWithExistingEmail);
            subView.EmailInput.onValueChanged.RemoveListener(OnEmailInputValueChanged);
            fetchProgramRegistrationInfoCts.SafeCancelAndDispose();
            registerInTheProgramCts.SafeCancelAndDispose();
        }

        private async UniTask LoadProgramRegistrationInfoAsync(CancellationToken ct)
        {
            try
            {
                subView.SetAsLoading(true);

                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    currentCreditsProgramProgress = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);
                    RedirectToSection();
                }

                subView.SetAsLoading(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the Credits Program. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void RegisterInTheProgramWithNewEmail()
        {
            registerInTheProgramCts = registerInTheProgramCts.SafeRestart();
            RegisterInTheProgramWithNewEmailAsync(
                string.IsNullOrEmpty(currentCreditsProgramProgress.user.email) ? subView.EmailInput.text : currentCreditsProgramProgress.user.email,
                registerInTheProgramCts.Token).Forget();
        }

        private async UniTaskVoid RegisterInTheProgramWithNewEmailAsync(string email, CancellationToken ct)
        {
            try
            {
                subView.SetAsLoading(true);
                await marketplaceCreditsAPIClient.SubscribeEmailAsync(email, ct);
                currentCreditsProgramProgress.user.email = email;
                currentCreditsProgramProgress.user.isEmailConfirmed = false;
                RedirectToSection(ignoreHasUserStartedProgramFlag: true);
                marketplaceCreditsMenuController.SetSidebarButtonAnimationAsAlert(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error registering in the Credits Program. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                subView.SetAsLoading(false);
            }
        }

        private void RegisterInTheProgramWithExistingEmail()
        {
            registerInTheProgramCts = registerInTheProgramCts.SafeRestart();
            RegisterInTheProgramWithExistingEmailAsync(registerInTheProgramCts.Token).Forget();
        }

        private async UniTaskVoid RegisterInTheProgramWithExistingEmailAsync(CancellationToken ct)
        {
            try
            {
                subView.SetAsLoading(true);

                if (currentCreditsProgramProgress.IsUserEmailVerified())
                {
                    await marketplaceCreditsAPIClient.MarkUserAsStartedProgramAsync(ct);
                    await LoadProgramRegistrationInfoAsync(ct);
                }
                else
                    RedirectToSection(ignoreHasUserStartedProgramFlag: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error registering in the Credits Program. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                subView.SetAsLoading(false);
            }
        }

        private void RedirectToSection(bool ignoreHasUserStartedProgramFlag = false)
        {
            subView.IsEmailLoginActive = false;
            totalCreditsWidgetView.SetCredits(MarketplaceCreditsUtils.FormatTotalCredits(currentCreditsProgramProgress.credits.available));
            totalCreditsWidgetView.SetDaysToExpire(MarketplaceCreditsUtils.FormatCreditsExpireIn(currentCreditsProgramProgress.credits.expiresIn));
            totalCreditsWidgetView.SetDaysToExpireVisible(currentCreditsProgramProgress.credits.available > 0);

            // PROGRAM ENDED FLOW
            if (currentCreditsProgramProgress.IsProgramEnded())
            {
                marketplaceCreditsProgramEndedSubController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.PROGRAM_ENDED);
                totalCreditsWidgetView.gameObject.SetActive(currentCreditsProgramProgress.season.seasonState != nameof(MarketplaceCreditsUtils.SeasonState.ERR_PROGRAM_PAUSED));
                return;
            }

            totalCreditsWidgetView.gameObject.SetActive(true);

            // NON-REGISTERED USER FLOW
            if (!currentCreditsProgramProgress.IsUserEmailRegistered())
            {
                subView.IsEmailLoginActive = true;
                inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
                return;
            }

            if (!ignoreHasUserStartedProgramFlag && !currentCreditsProgramProgress.HasUserStartedProgram())
                return;

            // REGISTERED BUT NON-VERIFIED USER FLOW
            if (!currentCreditsProgramProgress.IsUserEmailVerified())
            {
                marketplaceCreditsVerifyEmailSubController.Setup(currentCreditsProgramProgress.user.email);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.VERIFY_EMAIL);
                return;
            }

            // ALREADY REGISTERED USER FLOW
            if (currentCreditsProgramProgress.AreWeekGoalsCompleted())
            {
                marketplaceCreditsWeekGoalsCompletedSubController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.WEEK_GOALS_COMPLETED);
            }
            else
            {
                marketplaceCreditsGoalsOfTheWeekSubController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
            }
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsMenuController.WEEKLY_REWARDS_INFO_LINK);

        private void OnEmailInputValueChanged(string email)
        {
            CheckStartWithEmailButtonState();
            subView.ShowEmailError(!string.IsNullOrEmpty(email) && !IsValidEmail(email));
        }

        private void CheckStartWithEmailButtonState() =>
            subView.SetStartWithEmailButtonInteractable(IsValidEmail(subView.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && EMAIL_PATTERN_REGEX.IsMatch(email);
    }
}
