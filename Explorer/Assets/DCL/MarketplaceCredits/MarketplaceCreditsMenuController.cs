using Cysharp.Threading.Tasks;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuController : IDisposable
    {
        private readonly MarketplaceCreditsMenuView view;
        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly ISidebarBus sidebarBus;

        private CancellationTokenSource marketplaceCreditsCts;

        public MarketplaceCreditsMenuController(
            MarketplaceCreditsMenuView view,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            ISidebarBus sidebarBus)
        {
            this.sidebarButton = sidebarButton;
            this.view = view;
            this.sidebarBus = sidebarBus;

            view.CloseButton.onClick.AddListener(ClosePanel);
        }

        public void ToggleMarketplaceCreditsPanel(bool forceClose)
        {
            marketplaceCreditsCts = marketplaceCreditsCts.SafeRestart();

            if (!forceClose && !view.gameObject.activeSelf)
                view.ShowAsync(marketplaceCreditsCts.Token).Forget();
            else if (view.gameObject.activeSelf)
            {
                view.HideAsync(marketplaceCreditsCts.Token).Forget();
                sidebarButton.Deselect();
            }
        }

        public void Dispose()
        {
            marketplaceCreditsCts.SafeCancelAndDispose();
            view.CloseButton.onClick.RemoveListener(ClosePanel);
        }

        private void ClosePanel()
        {
            sidebarBus.UnblockSidebar();
            marketplaceCreditsCts = marketplaceCreditsCts.SafeRestart();
            view.HideAsync(marketplaceCreditsCts.Token).Forget();
            sidebarButton.Deselect();
        }
    }
}
