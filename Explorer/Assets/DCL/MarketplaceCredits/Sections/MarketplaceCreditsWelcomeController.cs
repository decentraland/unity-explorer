using DCL.Browser;
using DCL.MarketplaceCredits.Fields;
using System;
using System.Text.RegularExpressions;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWelcomeController : IDisposable
    {
        private const string LEARN_MORE_LINK = "https://docs.decentraland.org/";
        private const string EMAIL_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        private readonly MarketplaceCreditsWelcomeView view;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsWelcomeController(
            MarketplaceCreditsWelcomeView view,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.webBrowser = webBrowser;

            view.LearnMoreLinkButton.onClick.AddListener(OpenLearnMoreLink);
            view.StartButton.onClick.AddListener(GoToGoalsOfTheWeek);
            view.StartWithEmailButton.onClick.AddListener(RegisterEmail);
            view.EmailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
        }

        public void OnOpenSection()
        {
            // TODO (SANTI): Check if we have already an email registered
            // ...

            view.IsEmailLoginActive = false;
            view.CleanSection();
            CheckStartWithEmailButtonState();
        }

        public void Dispose()
        {
            view.LearnMoreLinkButton.onClick.RemoveListener(OpenLearnMoreLink);
            view.StartButton.onClick.RemoveListener(GoToGoalsOfTheWeek);
            view.StartWithEmailButton.onClick.RemoveListener(RegisterEmail);
            view.EmailInput.onValueChanged.RemoveListener(OnEmailInputValueChanged);
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
            view.ShowEmailError(!IsValidEmail(email));
            CheckStartWithEmailButtonState();
        }

        private void CheckStartWithEmailButtonState() =>
            view.SetStartWithEmailButtonInteractable(IsValidEmail(view.EmailInput.text));

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && Regex.IsMatch(email, EMAIL_PATTERN);
    }
}
