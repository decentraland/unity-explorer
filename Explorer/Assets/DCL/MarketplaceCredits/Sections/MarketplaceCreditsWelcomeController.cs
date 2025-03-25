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

        private Profile ownProfile;
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
            view.StartButton.onClick.AddListener(RegisterInTheProgram);
            view.StartWithEmailButton.onClick.AddListener(RegisterInTheProgram);
            view.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OpenSection()
        {
            view.gameObject.SetActive(true);

            fetchProgramRegistrationInfoCts = fetchProgramRegistrationInfoCts.SafeRestart();
            LoadProgramRegistrationInfoAsync(fetchProgramRegistrationInfoCts.Token).Forget();

            view.IsEmailLoginActive = false;
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
            view.StartButton.onClick.RemoveListener(RegisterInTheProgram);
            view.StartWithEmailButton.onClick.RemoveListener(RegisterInTheProgram);
            view.EmailInput.onValueChanged.RemoveListener(OnEmailInputValueChanged);
            fetchProgramRegistrationInfoCts.SafeCancelAndDispose();
            registerInTheProgramCts.SafeCancelAndDispose();
        }

        private async UniTaskVoid LoadProgramRegistrationInfoAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);

                ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    currentCreditsProgramProgress = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);
                    RedirectToSection(currentCreditsProgramProgress);
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

        private void RegisterInTheProgram()
        {
            if (ownProfile == null)
                return;

            registerInTheProgramCts = registerInTheProgramCts.SafeRestart();
            RegisterInTheProgramAsync(
                ownProfile.UserId,
                string.IsNullOrEmpty(currentCreditsProgramProgress.user.email) ? view.EmailInput.text : currentCreditsProgramProgress.user.email,
                registerInTheProgramCts.Token).Forget();
        }

        private async UniTaskVoid RegisterInTheProgramAsync(string walletId, string email, CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);
                var programRegistrationResponse = await marketplaceCreditsAPIClient.RegisterInTheProgramAsync(walletId, email, ct);
                RedirectToSection(programRegistrationResponse);
                view.SetAsLoading(false);
                marketplaceCreditsMenuController.SetSidebarButtonAnimationAsAlert(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error registering in the Credits Program. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void RedirectToSection(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            totalCreditsWidgetView.SetCredits(MarketplaceCreditsUtils.FormatTotalCredits(creditsProgramProgressResponse.credits.available));
            totalCreditsWidgetView.SetDaysToExpire(MarketplaceCreditsUtils.FormatCreditsExpireIn(creditsProgramProgressResponse.credits.expireIn));
            totalCreditsWidgetView.SetDaysToExpireVisible(creditsProgramProgressResponse.credits.available > 0);

            // PROGRAM ENDED FLOW
            if (creditsProgramProgressResponse.IsProgramEnded())
            {
                marketplaceCreditsProgramEndedController.Setup(creditsProgramProgressResponse);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.PROGRAM_ENDED);
                return;
            }

            // NON-REGISTERED USER FLOW
            if (!creditsProgramProgressResponse.IsUserEmailRegistered())
            {
                view.IsEmailLoginActive = string.IsNullOrEmpty(creditsProgramProgressResponse.user.email);
                if (view.IsEmailLoginActive) inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
                return;
            }

            // NON-VERIFIED USER FLOW
            if (!creditsProgramProgressResponse.IsUserEmailVerified())
            {
                marketplaceCreditsVerifyEmailController.Setup(creditsProgramProgressResponse.user.email);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.VERIFY_EMAIL);
                return;
            }

            // ALREADY REGISTERED USER FLOW
            if (creditsProgramProgressResponse.AreWeekGoalsCompleted())
            {
                marketplaceCreditsWeekGoalsCompletedController.Setup(creditsProgramProgressResponse);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.WEEK_GOALS_COMPLETED);
            }
            else if (ownProfile != null)
            {
                marketplaceCreditsGoalsOfTheWeekController.Setup(ownProfile.UserId, creditsProgramProgressResponse);
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
            }
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.LEARN_MORE_LINK);

        private void OnEmailInputValueChanged(string email)
        {
            CheckStartWithEmailButtonState();
            view.ShowEmailError(!string.IsNullOrEmpty(email) & !IsValidEmail(email));
        }

        private void CheckStartWithEmailButtonState() =>
            view.SetStartWithEmailButtonInteractable(IsValidEmail(view.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
