using System;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalsOfTheWeekController : IDisposable
    {
        private readonly MarketplaceCreditsGoalsOfTheWeekView view;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        public MarketplaceCreditsGoalsOfTheWeekController(
            MarketplaceCreditsGoalsOfTheWeekView view,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.view = view;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
        }

        public void Dispose()
        {

        }
    }
}
