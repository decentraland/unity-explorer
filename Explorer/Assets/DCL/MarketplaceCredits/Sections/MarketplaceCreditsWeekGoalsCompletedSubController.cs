using DCL.MarketplaceCreditsAPIService;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWeekGoalsCompletedSubController : IDisposable
    {
        private readonly MarketplaceCreditsWeekGoalsCompletedSubView subView;

        public MarketplaceCreditsWeekGoalsCompletedSubController(MarketplaceCreditsWeekGoalsCompletedSubView subView)
        {
            this.subView = subView;
        }

        public void OpenSection() =>
            subView.gameObject.SetActive(true);

        public void CloseSection() =>
            subView.gameObject.SetActive(false);

        public void Setup(CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            subView.SetTimeLeftText(MarketplaceCreditsUtils.FormatEndOfTheWeekDate(creditsProgramProgressResponse.currentWeek.timeLeft));

        public void Dispose() { }
    }
}
