using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
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
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;

        private Profile ownProfile;
        private CancellationTokenSource fetchProgramRegistrationInfoCts;
        private CancellationTokenSource registerInTheProgramCts;

        public MarketplaceCreditsWelcomeController(
            MarketplaceCreditsWelcomeView view,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;

            view.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            view.StartButton.onClick.AddListener(RegisterInTheProgram);
            view.StartWithEmailButton.onClick.AddListener(RegisterInTheProgram);
            view.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OnOpenSection()
        {
            fetchProgramRegistrationInfoCts = fetchProgramRegistrationInfoCts.SafeRestart();
            LoadProgramRegistrationInfoAsync(fetchProgramRegistrationInfoCts.Token).Forget();

            view.IsEmailLoginActive = false;
            view.CleanEmailInput();
            CheckStartWithEmailButtonState();
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
                    var programRegistrationResponse = await marketplaceCreditsAPIClient.GetProgramRegistrationInfoAsync(ownProfile.UserId, ct);
                    RedirectToSection(programRegistrationResponse);
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
            RegisterInTheProgramAsync(ownProfile.UserId, registerInTheProgramCts.Token).Forget();
        }

        private async UniTaskVoid RegisterInTheProgramAsync(string walletId, CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);
                var programRegistrationResponse = await marketplaceCreditsAPIClient.RegisterInTheProgramAsync(walletId, ct);
                RedirectToSection(programRegistrationResponse);
                view.SetAsLoading(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error registering in the Credits Program. Please try again!";
                marketplaceCreditsMenuController.ShowErrorNotification(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void RedirectToSection(ProgramRegistrationResponse programRegistrationResponse)
        {
            totalCreditsWidgetView.SetCredits(MarketplaceCreditsUtils.FormatTotalCredits(programRegistrationResponse.totalCredits));
            totalCreditsWidgetView.SetDaysToExpire(MarketplaceCreditsUtils.FormatDaysToCreditsExpire(programRegistrationResponse.daysToExpire));
            totalCreditsWidgetView.SetDaysToExpireVisible(programRegistrationResponse.totalCredits > 0);

            if (programRegistrationResponse.isProgramEnded)
            {
                marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.PROGRAM_ENDED);
                return;
            }

            if (!programRegistrationResponse.isRegistered)
                return;

            marketplaceCreditsMenuController.OpenSection(
                programRegistrationResponse.areWeekGoalsCompleted ?
                    MarketplaceCreditsSection.WEEK_GOALS_COMPLETED :
                    MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.LEARN_MORE_LINK);

        private void OnEmailInputValueChanged(string email)
        {
            view.ShowEmailError(!IsValidEmail(email));
            CheckStartWithEmailButtonState();
        }

        private void CheckStartWithEmailButtonState() =>
            view.SetStartWithEmailButtonInteractable(IsValidEmail(view.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
