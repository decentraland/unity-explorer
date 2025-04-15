using DCL.MarketplaceCreditsAPIService;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWeekGoalsCompletedController : IDisposable
    {
        private readonly MarketplaceCreditsWeekGoalsCompletedView view;

        public MarketplaceCreditsWeekGoalsCompletedController(MarketplaceCreditsWeekGoalsCompletedView view)
        {
            this.view = view;
        }

        public void OpenSection() =>
            view.gameObject.SetActive(true);

        public void CloseSection() =>
            view.gameObject.SetActive(false);

        public void Setup(CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            view.SetTimeLeftText(MarketplaceCreditsUtils.FormatEndOfTheWeekDate(creditsProgramProgressResponse.currentWeek.timeLeft));

        public void Dispose() { }
    }
}
