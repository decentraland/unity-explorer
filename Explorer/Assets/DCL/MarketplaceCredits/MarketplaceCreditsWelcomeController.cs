using System;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsWelcomeController : IDisposable
    {
        private readonly MarketplaceCreditsWelcomeView welcomeView;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        public MarketplaceCreditsWelcomeController(
            MarketplaceCreditsWelcomeView welcomeView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.welcomeView = welcomeView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;

            welcomeView.StartButton.onClick.AddListener(StartCreditsProgram);
        }

        private void StartCreditsProgram()
        {
            marketplaceCreditsMenuController.OpenSectionView(MarketplaceCreditsSection.GOALS_OF_THE_WEEK);
        }

        public void Dispose()
        {
            welcomeView.StartButton.onClick.RemoveAllListeners();
        }
    }
}
