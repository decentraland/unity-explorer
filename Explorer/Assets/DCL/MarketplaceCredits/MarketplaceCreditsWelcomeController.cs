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

            welcomeView.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            welcomeView.StartButton.onClick.AddListener(GoToGoalsOfTheWeek);
            welcomeView.StartWithEmailButton.onClick.AddListener(RegisterEmail);
            welcomeView.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OnOpenSection()
        {
            // TODO (SANTI): Check if we have already an email registered
            // ...

            welcomeView.IsEmailLoginActive = false;
            welcomeView.CleanEmail();
            CheckStartWithEmailButtonState();
        }

        public void Dispose()
        {
            welcomeView.StartButton.onClick.RemoveAllListeners();
            welcomeView.StartWithEmailButton.onClick.RemoveAllListeners();
            welcomeView.LearnMoreLinkButton.onClick.RemoveAllListeners();
            welcomeView.EmailInput.onValueChanged.RemoveAllListeners();
        }

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(LEARN_MORE_LINK);

        private void GoToGoalsOfTheWeek() =>
            marketplaceCreditsMenuController.OpenSection(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);

        private void RegisterEmail()
        {
            // TODO (SANTI): Implement Email Login flow
        }

        private void OnEmailInputValueChanged(string email)
        {
            welcomeView.ShowEmailError(!IsValidEmail(email));
            CheckStartWithEmailButtonState();
        }

        private void CheckStartWithEmailButtonState() =>
            welcomeView.SetStartWithEmailButtonInteractable(IsValidEmail(welcomeView.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
