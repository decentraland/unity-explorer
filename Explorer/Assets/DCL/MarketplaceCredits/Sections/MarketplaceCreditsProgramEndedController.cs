using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedController : IDisposable
    {
        private readonly MarketplaceCreditsProgramEndedView view;

        public MarketplaceCreditsProgramEndedController(MarketplaceCreditsProgramEndedView view)
        {
            this.view = view;
        }

        public void OpenSection() =>
            view.gameObject.SetActive(true);

        public void CloseSection() =>
            view.gameObject.SetActive(false);

        public void Dispose() { }
    }
}
