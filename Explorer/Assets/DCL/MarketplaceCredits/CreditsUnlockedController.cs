using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits
{
    public class CreditsUnlockedController : ControllerBase<CreditsUnlockedView>
    {
        public event Action OnClosePanel;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CreditsUnlockedController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                UniTask.Delay(MarketplaceCreditsUtils.CREDITS_UNLOCKED_DURATION * 1000, cancellationToken: ct));

        protected override void OnViewClose() =>
            OnClosePanel?.Invoke();
    }
}
