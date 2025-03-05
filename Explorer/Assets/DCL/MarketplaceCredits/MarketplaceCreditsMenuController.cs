using Cysharp.Threading.Tasks;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using System;
using System.Threading;
using UnityEngine.UI;
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

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);
        }

        public void OpenPanel()
        {
            if (view.gameObject.activeSelf)
                return;

            marketplaceCreditsCts = marketplaceCreditsCts.SafeRestart();
            view.ShowAsync(marketplaceCreditsCts.Token).Forget();
        }

        public void ClosePanel()
        {
            if (!view.gameObject.activeSelf)
                return;

            sidebarBus.UnblockSidebar();
            marketplaceCreditsCts = marketplaceCreditsCts.SafeRestart();
            view.HideAsync(marketplaceCreditsCts.Token).Forget();
            sidebarButton.Deselect();
        }

        public void Dispose()
        {
            marketplaceCreditsCts.SafeCancelAndDispose();

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveListener(ClosePanel);
        }
    }
}
