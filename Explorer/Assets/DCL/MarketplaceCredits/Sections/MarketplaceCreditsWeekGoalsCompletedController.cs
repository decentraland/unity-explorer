using DCL.MarketplaceCredits.Fields;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWeekGoalsCompletedController : IDisposable
    {
        private readonly MarketplaceCreditsWeekGoalsCompletedView view;
        private readonly MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView;

        public MarketplaceCreditsWeekGoalsCompletedController(
            MarketplaceCreditsWeekGoalsCompletedView view,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView)
        {
            this.view = view;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
        }

        public void OnOpenSection() =>
            totalCreditsWidgetView.gameObject.SetActive(true);

        public void Setup(string endOfTheWeekDate) =>
            view.TimeLeftText.text = MarketplaceCreditsUtils.FormatEndOfTheWeekDateTimestamp(endOfTheWeekDate);

        public void Dispose() { }
    }
}
