using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWelcomeController : IDisposable
    {
        private const string EMAIL_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        private const string LEARN_MORE_LINK = "https://docs.decentraland.org";

        private readonly MarketplaceCreditsWelcomeView view;
        private readonly MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly MarketplaceCreditsVerifyEmailController marketplaceCreditsVerifyEmailController;
        private readonly MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;
        private readonly MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController;
        private readonly MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;

        private CreditsProgramProgressResponse currentCreditsProgramProgress;
        private CancellationTokenSource fetchProgramRegistrationInfoCts;
        private CancellationTokenSource registerInTheProgramCts;

        public MarketplaceCreditsWelcomeController(
            MarketplaceCreditsWelcomeView view,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            MarketplaceCreditsVerifyEmailController marketplaceCreditsVerifyEmailController,
            MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController,
            MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController,
            MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.marketplaceCreditsVerifyEmailController = marketplaceCreditsVerifyEmailController;
            this.marketplaceCreditsGoalsOfTheWeekController = marketplaceCreditsGoalsOfTheWeekController;
            this.marketplaceCreditsWeekGoalsCompletedController = marketplaceCreditsWeekGoalsCompletedController;
            this.marketplaceCreditsProgramEndedController = marketplaceCreditsProgramEndedController;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;

            view.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            view.StartWithEmailButton.onClick.AddListener(RegisterInTheProgramWithNewEmail);
            view.StartButton.onClick.AddListener(RegisterInTheProgramWithExistingEmail);
            view.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OpenSection()
        {
            view.gameObject.SetActive(true);

            fetchProgramRegistrationInfoCts = fetchProgramRegistrationInfoCts.SafeRestart();
            LoadProgramRegistrationInfoAsync(fetchProgramRegistrationInfoCts.Token).Forget();

            view.CleanEmailInput();
            OnEmailInputValueChanged(view.EmailInput.text);
            CheckStartWithEmailButtonState();
        }

        public void CloseSection()
        {
            view.gameObject.SetActive(false);
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void Dispose()
        {
            view.LearnMoreLinkButton.onClick.RemoveListener(OpenLearnMoreLink);
            view.StartWithEmailButton.onClick.RemoveListener(RegisterInTheProgramWithNewEmail);
            view.StartButton.onClick.RemoveListener(RegisterInTheProgramWithExistingEmail);
            view.EmailInput.onValueChanged.RemoveListener(OnEmailInputValueChanged);
            fetchProgramRegistrationInfoCts.SafeCancelAndDispose();
            registerInTheProgramCts.SafeCancelAndDispose();
        }

        private async UniTaskVoid LoadProgramRegistrationInfoAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);

                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    currentCreditsProgramProgress = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);
                    RedirectToSection();
                }

                view.SetAsLoading(false);
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
                string.IsNullOrEmpty(currentCreditsProgramProgress.user.email) ? view.EmailInput.text : currentCreditsProgramProgress.user.email,
                registerInTheProgramCts.Token).Forget();
        }

        private async UniTaskVoid RegisterInTheProgramWithNewEmailAsync(string email, CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);
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
                view.SetAsLoading(false);
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
                view.SetAsLoading(true);

                if (currentCreditsProgramProgress.IsUserEmailVerified())
                    await marketplaceCreditsAPIClient.MarkUserAsStartedProgramAsync(ct);

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
                view.SetAsLoading(false);
            }
        }

        private void RedirectToSection(bool ignoreHasUserStartedProgramFlag = false)
        {
            view.IsEmailLoginActive = false;
            totalCreditsWidgetView.SetCredits(MarketplaceCreditsUtils.FormatTotalCredits(currentCreditsProgramProgress.credits.available));
            totalCreditsWidgetView.SetDaysToExpire(MarketplaceCreditsUtils.FormatCreditsExpireIn(currentCreditsProgramProgress.credits.expiresIn));
            totalCreditsWidgetView.SetDaysToExpireVisible(currentCreditsProgramProgress.credits.available > 0);

            // PROGRAM ENDED FLOW
            if (currentCreditsProgramProgress.IsProgramEnded())
            {
                marketplaceCreditsProgramEndedController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.PROGRAM_ENDED);
                return;
            }

            // NON-REGISTERED USER FLOW
            if (!currentCreditsProgramProgress.IsUserEmailRegistered())
            {
                view.IsEmailLoginActive = true;
                inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
                return;
            }

            if (!ignoreHasUserStartedProgramFlag && !currentCreditsProgramProgress.HasUserStartedProgram())
                return;

            // REGISTERED BUT NON-VERIFIED USER FLOW
            if (!currentCreditsProgramProgress.IsUserEmailVerified())
            {
                marketplaceCreditsVerifyEmailController.Setup(currentCreditsProgramProgress.user.email);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.VERIFY_EMAIL);
                return;
            }

            // ALREADY REGISTERED USER FLOW
            if (currentCreditsProgramProgress.AreWeekGoalsCompleted())
            {
                marketplaceCreditsWeekGoalsCompletedController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.WEEK_GOALS_COMPLETED);
            }
            else
            {
                marketplaceCreditsGoalsOfTheWeekController.Setup(currentCreditsProgramProgress);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
            }
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(LEARN_MORE_LINK);

        private void OnEmailInputValueChanged(string email)
        {
            CheckStartWithEmailButtonState();
            view.ShowEmailError(!string.IsNullOrEmpty(email) && !IsValidEmail(email));
        }

        private void CheckStartWithEmailButtonState() =>
            view.SetStartWithEmailButtonInteractable(IsValidEmail(view.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
