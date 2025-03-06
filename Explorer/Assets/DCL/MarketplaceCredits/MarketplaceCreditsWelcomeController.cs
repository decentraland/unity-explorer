using DCL.Browser;
using System;
using System.Text.RegularExpressions;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsWelcomeController : IDisposable
    {
        private const string LEARN_MORE_LINK = "https://docs.decentraland.org/";
        private const string EMAIL_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        private readonly MarketplaceCreditsWelcomeView welcomeView;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsWelcomeController(
            MarketplaceCreditsWelcomeView welcomeView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            IWebBrowser webBrowser)
        {
            this.welcomeView = welcomeView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.webBrowser = webBrowser;

            welcomeView.StartButton.onClick.AddListener(StartCreditsProgram);
            welcomeView.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            welcomeView.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OnOpenSection()
        {
            // TODO (SANTI): Check if we have to show the login email section or not (depending on if the user already has an email registered or not)
            welcomeView.IsEmailLoginActive = false;
            welcomeView.CleanEmail();
            CheckStartButtonState();
        }

        public void Dispose()
        {
            welcomeView.StartButton.onClick.RemoveAllListeners();
            welcomeView.LearnMoreLinkButton.onClick.RemoveAllListeners();
            welcomeView.EmailInput.onValueChanged.RemoveAllListeners();
        }

        private void StartCreditsProgram()
        {
            // TODO (SANTI): Send the email to the server (if it exists)
            marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(LEARN_MORE_LINK);

        private void OnEmailInputValueChanged(string email)
        {
            welcomeView.ShowEmailError(!IsValidEmail(email));
            CheckStartButtonState();
        }

        private void CheckStartButtonState()
        {
            var canStart = true;

            if (welcomeView.IsEmailLoginActive)
                canStart = IsValidEmail(welcomeView.EmailInput.text);

            welcomeView.SetStartButtonInteractable(canStart);
        }

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
