using DCL.MarketplaceCredits.Fields;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedController : IDisposable
    {
        private readonly MarketplaceCreditsProgramEndedView view;
        private readonly MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView;

        public MarketplaceCreditsProgramEndedController(
            MarketplaceCreditsProgramEndedView view,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView)
        {
            this.view = view;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
        }

        public void OnOpenSection() =>
            totalCreditsWidgetView.gameObject.SetActive(true);

        public void Dispose() { }
    }
}
